using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TupZak.Demo.Agent;
using TupZak.Demo.Services;
using TupZak.Demo.Tools;

namespace TupZak.Demo.Mcp;

/// <summary>
/// MCP App (rozszerzenie io.modelcontextprotocol/ui): narzędzie show_building_map wskazuje w _meta.ui.resourceUri
/// zasób ui:// z HTML-em. Host obsługujący MCP Apps (np. Claude Desktop) renderuje go w iframe w rozmowie
/// i przekazuje mu wynik narzędzia. Host bez MCP Apps (np. Claude Code w terminalu) widzi tylko tekst wyniku.
/// </summary>
[McpServerToolType]
[McpServerResourceType]
public sealed class CampusMapApp
{
    public const string ShowBuildingMapName = "show_building_map";
    public const string ShowDeliveryPlanName = "show_delivery_plan";
    public const string MapViewUri = "ui://tupzak/building-map.html";

    private static readonly Lazy<string> MapViewHtml = new(() =>
    {
        using var stream = typeof(CampusMapApp).Assembly.GetManifestResourceStream("TupZak.Demo.Mcp.BuildingMapView.html")
            ?? throw new InvalidOperationException("Brak osadzonego zasobu BuildingMapView.html.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    });

    [McpServerTool(Name = ShowBuildingMapName, Title = "Pokaż mapę budynku", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [McpAppUi(ResourceUri = MapViewUri)]
    [Description("Wyświetla w rozmowie mapę budynku z otoczeniem (dane GeoJSON). Opcjonalnie wyróżnia jedno wejście.")]
    public static CallToolResult ShowBuildingMap(
        McpServer server,
        ILogger<CampusMapApp> logger,
        BuildingRepository repository,
        [Description("Identyfikator budynku zwrócony przez find_building, np. \"A\".")] string buildingId,
        [Description("Identyfikator wejścia tego budynku do wyróżnienia, np. \"A1\". Pomiń, żeby pokazać sam budynek.")] string? entranceId = null)
    {
        // Host bez rozszerzenia io.modelcontextprotocol/ui nie wyświetli widoku, zobaczy tylko tekst wyniku.
        var uiCapability = McpApps.GetUiCapability(server.ClientCapabilities);
        logger.LogInformation("show_building_map: klient {Client} {Support} MCP Apps (MIME: {MimeTypes}).",
            server.ClientInfo?.Name, uiCapability is null ? "nie obsługuje" : "obsługuje",
            string.Join(", ", uiCapability?.MimeTypes ?? []));

        var building = repository.GetById(buildingId);
        if (building is null)
            return Error($"Budynek o identyfikatorze \"{buildingId}\" nie istnieje. Dostępne budynki: {string.Join(", ", repository.All.Select(b => b.Id))}.");

        MapFeature? entrance = null;
        if (!string.IsNullOrWhiteSpace(entranceId))
        {
            entrance = building.Entrances.FirstOrDefault(e => e.Id.Equals(entranceId.Trim(), StringComparison.OrdinalIgnoreCase));
            if (entrance is null)
                return Error($"Wejście \"{entranceId}\" nie należy do budynku {building.Id}. Wejścia budynku: {string.Join(", ", building.Entrances.Select(e => e.Id))}.");
        }

        var structured = new JsonObject
        {
            ["buildingId"] = building.Id,
            ["name"] = building.Name,
            ["entranceId"] = entrance?.Id,
            ["geojson"] = building.GeoJson.DeepClone(),
        };

        string text = (entrance is null
            ? $"Wyświetlono mapę: {building.Name} (wejścia: {string.Join(", ", building.Entrances.Select(e => e.Id))})."
            : $"Wyświetlono mapę: {building.Name} z wyróżnionym wejściem {entrance.Id}.")
            + " Mapa jest widoczna w rozmowie jako widget, nie trzeba otwierać strony aplikacji.";

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, CampusTools.JsonOptions),
        };
    }

    /// <summary>
    /// Ten sam widok mapy, rozszerzony o plan dostaw: cały kampus, trasa po ścieżkach i ponumerowane punkty.
    /// Kolejność podaje wywołujący, więc narzędzie pokaże wynik optimize_deliveries, skryptu Skilla albo kodu agenta.
    /// </summary>
    [McpServerTool(Name = ShowDeliveryPlanName, Title = "Pokaż plan dostaw", ReadOnly = false, Idempotent = true, OpenWorld = false)]
    [McpAppUi(ResourceUri = MapViewUri)]
    [Description("Wyświetla w rozmowie mapę kampusu z trasą dostaw: punkt startowy, ponumerowane wejścia w kolejności odwiedzania " +
                 "i przebieg trasy po ścieżkach. Kolejność wejść podaj sam, np. z wyniku optimize_deliveries.")]
    public static CallToolResult ShowDeliveryPlan(
        BuildingRepository repository,
        CampusNetwork network,
        DeliveryPlanner planner,
        MapState mapState,
        [Description("Punkt startowy, np. \"A-P1\".")] string start,
        [Description("Identyfikatory wejść w kolejności odwiedzania, np. [\"A2\", \"B1\", \"C1\"].")] string[] entranceIds,
        [Description("true, jeżeli trasa wraca do punktu startowego.")] bool returnToStart = false,
        [Description("true, jeżeli kurier ma wózek: trasa omija schody.")] bool requiresCart = false,
        [Description("Identyfikatory ścieżek do ominięcia, np. [\"W-02\"].")] string[]? avoidPathIds = null)
    {
        var plan = planner.Evaluate(start, entranceIds, returnToStart, requiresCart, avoidPathIds);
        if (!plan.Success)
            return Error(plan.Error!);

        mapState.ShowDeliveryPlan(plan);

        var features = new JsonArray();
        foreach (var building in repository.All)
            foreach (var feature in building.GeoJson["features"]!.AsArray())
                features.Add(feature!.DeepClone());
        foreach (var path in network.Paths.Values.Where(p => !repository.All.Any(b => b.Features.Any(f => f.Id == p.Id))))
            features.Add(new JsonObject
            {
                ["type"] = "Feature",
                ["properties"] = new JsonObject { ["id"] = path.Id, ["kind"] = "path", ["name"] = path.Name, ["accessible"] = path.Accessible },
                ["geometry"] = new JsonObject
                {
                    ["type"] = "LineString",
                    ["coordinates"] = new JsonArray(path.Points.Select(p => (JsonNode)new JsonArray(p.X, p.Y)).ToArray()),
                },
            });

        var structured = new JsonObject
        {
            ["geojson"] = new JsonObject { ["type"] = "FeatureCollection", ["features"] = features },
            ["plan"] = JsonSerializer.SerializeToNode(plan, CampusTools.JsonOptions),
        };

        string order = string.Join(" → ", [plan.Start!.Id, .. plan.Stops!.Select(s => s.EntranceId), .. (returnToStart ? new[] { plan.Start.Id } : [])]);
        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = $"Wyświetlono plan dostaw: {order}, łącznie {plan.TotalDistanceM} m. Mapa jest widoczna w rozmowie jako widget." }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, CampusTools.JsonOptions),
        };
    }

    [McpServerResource(UriTemplate = MapViewUri, Name = "building-map", Title = "Widok mapy budynku", MimeType = McpApps.HtmlMimeType)]
    [Description("Widok MCP App rysujący GeoJSON jako SVG: budynek (show_building_map) albo kampus z planem dostaw (show_delivery_plan).")]
    public static string BuildingMapView() => MapViewHtml.Value;

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new ToolError(message), CampusTools.JsonOptions) }],
    };
}
