using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TupZak.Demo.Agent;
using TupZak.Demo.Tools;

namespace TupZak.Demo.Mcp;

/// <summary>
/// Serwer MCP udostępniający te same narzędzia kampusu, z których korzysta Agent Loop z Ollama.
/// Hostem (pętlą agenta) jest tu klient MCP, np. Claude Code: sam pobiera listę narzędzi (tools/list)
/// i proponuje ich wywołania (tools/call). Aplikacja nadal waliduje argumenty i wykonuje kod C#.
/// </summary>
public static class CampusMcpServer
{
    public const string Instructions = """
        Serwer Tup Żak udostępnia dane fikcyjnego kampusu: budynki, wejścia, parkingi i ścieżki.
        Mapę budynku w rozmowie pokazuje narzędzie show_building_map (widget MCP App).
        Strona aplikacji w przeglądarce służy prowadzącemu demonstrację: nie otwieraj jej i nie rób jej zrzutów.

        Zasoby:
        - tupzak://buildings: pełna lista budynków kampusu z identyfikatorami wejść.
        - tupzak://buildings/{buildingId}: źródłowe dane GeoJSON budynku.
        - ui://tupzak/building-map.html: widok MCP App z mapą budynku (wyświetla go host po show_building_map).

        Zasady:
        - Na pytanie, jakie budynki ma kampus, odpowiadaj na podstawie zasobu tupzak://buildings.
        - Budynek znajdź narzędziem find_building, a jego geometrię i wejścia narzędziem get_building_geometry.
          Nie wymyślaj budynków, wejść, współrzędnych ani identyfikatorów.
        - Wejście „od strony” obiektu leży na elewacji (facade) zwróconej w kierunku tego obiektu
          (directionFromBuilding) i jest do niego najbliżej (nearby). connectedEntrances mówi, do których wejść
          prowadzi ścieżka.
        - Dane nie określają przodu ani tyłu budynku. Gdy opis nie wskazuje jednoznacznie jednego wejścia,
          nie wywołuj mark_entrance, tylko zapytaj użytkownika.
        - Gdy użytkownik chce zobaczyć budynek, mapę lub zaznaczone wejście, wywołaj show_building_map.
          W hostach obsługujących MCP Apps mapa pojawi się bezpośrednio w rozmowie; nie otwieraj innych stron.
        - Jeżeli narzędzie zwróci błąd, przekaż go użytkownikowi. Nie zgaduj.
        """;

    public static IServiceCollection AddCampusMcpServer(this IServiceCollection services)
    {
        services.AddSingleton<McpActivityLog>();

        services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation { Name = "tupzak", Title = "Tup Żak — kampus", Version = "1.0.0" };
                options.ServerInstructions = Instructions;
            })
            .WithHttpTransport()
            .WithResources<CampusResources>()
            .WithPrompts<CampusPrompts>()
            // MCP App: narzędzie show_building_map z widokiem ui:// (rozszerzenie io.modelcontextprotocol/ui).
            .WithTools<CampusMapApp>()
            .WithResources<CampusMapApp>()
            .WithMcpApps()
            .WithRequestFilters(filters => filters.AddCallToolFilter(LogAndValidate));

        // Te same Tool Definitions (AIFunction) co w Agent Loop, opakowane jako narzędzia MCP.
        foreach (string name in new[] { CampusTools.FindBuildingName, CampusTools.GetBuildingGeometryName, CampusTools.MarkEntranceName })
        {
            services.AddSingleton(sp =>
            {
                var function = sp.GetRequiredService<ToolExecutor>().Functions.Single(f => f.Name == name);
                return McpServerTool.Create(function, CreateOptions(name));
            });
        }

        return services;
    }

    private static McpServerToolCreateOptions CreateOptions(string name) => name switch
    {
        CampusTools.FindBuildingName => new() { Title = "Znajdź budynek", ReadOnly = true, Idempotent = true, OpenWorld = false },
        CampusTools.GetBuildingGeometryName => new() { Title = "Pobierz geometrię budynku", Destructive = false, Idempotent = true, OpenWorld = false },
        CampusTools.MarkEntranceName => new() { Title = "Zaznacz wejście na mapie", Destructive = false, Idempotent = true, OpenWorld = false },
        _ => new(),
    };

    /// <summary>
    /// Filtr tools/call: ta sama walidacja argumentów co w Agent Loop (<see cref="ToolExecutor.ValidateArguments"/>)
    /// oraz zapis wywołania i wyniku do <see cref="McpActivityLog"/>, żeby było widać je w przeglądarce.
    /// </summary>
    private static McpRequestHandler<CallToolRequestParams, CallToolResult> LogAndValidate(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) => async (context, cancellationToken) =>
    {
        var services = context.Services!;
        var log = services.GetRequiredService<McpActivityLog>();
        var tools = services.GetRequiredService<ToolExecutor>();

        string client = context.Server.ClientInfo?.Name ?? "klient MCP";
        string toolName = context.Params?.Name ?? "";
        var arguments = context.Params?.Arguments ?? new Dictionary<string, JsonElement>();
        string callId = context.JsonRpcRequest.Id.ToString();
        var stopwatch = Stopwatch.StartNew();

        log.Add(client, new AgentEvent
        {
            Type = "tool-call",
            CallId = callId,
            ToolName = toolName,
            Arguments = JsonSerializer.Serialize(arguments, CampusTools.JsonOptions),
        });

        CallToolResult result;
        var function = tools.Functions.FirstOrDefault(f => f.Name == toolName);
        string? validationError = function is null
            ? null
            : ToolExecutor.ValidateArguments(function, arguments.ToDictionary(a => a.Key, a => (object?)a.Value));

        if (validationError is not null)
        {
            // Model nie dostaje wyjątku, tylko opis błędu jako wynik narzędzia — tak samo jak w Agent Loop.
            result = new CallToolResult
            {
                IsError = true,
                Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new ToolError(validationError), CampusTools.JsonOptions) }],
            };
        }
        else
        {
            result = await next(context, cancellationToken);
        }

        string text = string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text));
        log.Add(client, new AgentEvent
        {
            Type = "tool-result",
            CallId = callId,
            ToolName = toolName,
            Result = text,
            Error = validationError ?? (result.IsError == true ? text : DomainError(text)),
            DurationMs = stopwatch.Elapsed.TotalMilliseconds,
        });

        return result;
    };

    /// <summary>Narzędzie może zakończyć się poprawnie, ale zgłosić błąd domenowy w polu "error".</summary>
    private static string? DomainError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement is { ValueKind: JsonValueKind.Object } root &&
                   root.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
