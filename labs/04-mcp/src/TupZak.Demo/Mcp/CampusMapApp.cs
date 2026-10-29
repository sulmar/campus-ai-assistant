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

    [McpServerResource(UriTemplate = MapViewUri, Name = "building-map", Title = "Widok mapy budynku", MimeType = McpApps.HtmlMimeType)]
    [Description("Widok MCP App rysujący GeoJSON budynku jako SVG. Wyświetlany przez host po wywołaniu show_building_map.")]
    public static string BuildingMapView() => MapViewHtml.Value;

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new ToolError(message), CampusTools.JsonOptions) }],
    };
}
