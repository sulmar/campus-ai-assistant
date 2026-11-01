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
    public const string AskCoffeeMachineLocationName = "ask_coffee_machine_location";
    public const string ReportCoffeeMachineLocationName = "report_coffee_machine_location";
    public const string FindCoffeeMachineName = "find_coffee_machine";
    public const string MapViewUri = "ui://tupzak/building-map.html";
    public const string PickCoffeeMachineMode = "pick-coffee-machine";

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
        CoffeeMachineReports coffeeMachines,
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
            ["coffeeMachine"] = CoffeeMachineJson(coffeeMachines.Get(building.Id)),
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
    /// Odpowiednik elicitacji z mapą: natywne elicitation/create obsługuje tylko proste pola formularza, więc o wskazanie
    /// prosi widget. Narzędzie kończy się od razu, a odpowiedź przychodzi w kolejnej turze jako wiadomość z widgetu.
    /// </summary>
    [McpServerTool(Name = AskCoffeeMachineLocationName, Title = "Poproś o wskazanie ekspresu na mapie", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [McpAppUi(ResourceUri = MapViewUri)]
    [Description("Wyświetla mapę budynku i prosi użytkownika o wskazanie na niej ekspresu do kawy. Użyj, gdy find_coffee_machine zwróci known: false.")]
    public static CallToolResult AskCoffeeMachineLocation(
        BuildingRepository repository,
        CoffeeMachineReports coffeeMachines,
        [Description("Identyfikator budynku zwrócony przez find_building, np. \"A\".")] string buildingId)
    {
        var building = repository.GetById(buildingId);
        if (building is null)
            return UnknownBuilding(buildingId, repository);

        var structured = new JsonObject
        {
            ["mode"] = PickCoffeeMachineMode,
            ["buildingId"] = building.Id,
            ["name"] = building.Name,
            ["geojson"] = building.GeoJson.DeepClone(),
            ["coffeeMachine"] = CoffeeMachineJson(coffeeMachines.Get(building.Id)),
        };

        // Bez rozgałęzienia po McpApps.GetUiCapability: transport HTTP jest bezstanowy, więc przy tools/call
        // serwer nie zna możliwości klienta z initialize. Model dostaje jedną instrukcję dla obu rodzajów hostów.
        string text = $"Mapa: {building.Name}. Poproś użytkownika o wskazanie ekspresu na mapie i zakończ turę; odpowiedź przyjdzie w osobnej wiadomości. "
            + "Mapa pojawia się tylko w hostach obsługujących MCP Apps: jeśli użytkownik jej nie widzi, poproś o opis słowny miejsca ekspresu. Nie zgaduj lokalizacji.";

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = text }],
            StructuredContent = JsonSerializer.SerializeToElement(structured, CampusTools.JsonOptions),
        };
    }

    /// <summary>Wywoływane tylko przez widget. Widoczność „app” egzekwuje host, więc serwer sam sprawdza dane.</summary>
    [McpServerTool(Name = ReportCoffeeMachineLocationName, Title = "Zapisz wskazanie ekspresu", Destructive = false, Idempotent = true, OpenWorld = false)]
    [McpAppUi(ResourceUri = MapViewUri, Visibility = [McpUiToolVisibility.App])]
    [Description("Zapisuje miejsce ekspresu do kawy wskazane przez użytkownika na mapie w widgecie.")]
    public static CallToolResult ReportCoffeeMachineLocation(
        BuildingRepository repository,
        CoffeeMachineReports coffeeMachines,
        [Description("Identyfikator budynku, np. \"A\".")] string buildingId,
        [Description("Współrzędna x w metrach (rośnie na wschód).")] double x,
        [Description("Współrzędna y w metrach (rośnie na północ).")] double y)
    {
        var building = repository.GetById(buildingId);
        if (building is null)
            return UnknownBuilding(buildingId, repository);

        var mapPoints = building.Outline.Concat(building.Entrances.Concat(building.Features).SelectMany(f => f.Points)).ToList();
        if (!(x >= mapPoints.Min(p => p.X) && x <= mapPoints.Max(p => p.X) && y >= mapPoints.Min(p => p.Y) && y <= mapPoints.Max(p => p.Y)))
            return Error($"Punkt ({x}, {y}) leży poza mapą budynku {building.Id}.");

        return CoffeeMachineResult(building, coffeeMachines.Report(building.Id, x, y),
            $"Zapisano miejsce ekspresu do kawy: {building.Name}.");
    }

    [McpServerTool(Name = FindCoffeeMachineName, Title = "Znajdź ekspres do kawy", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Zwraca miejsce ekspresu do kawy w budynku wskazane wcześniej przez użytkownika albo known: false, gdy nikt go jeszcze nie wskazał.")]
    public static CallToolResult FindCoffeeMachine(
        BuildingRepository repository,
        CoffeeMachineReports coffeeMachines,
        [Description("Identyfikator budynku zwrócony przez find_building, np. \"A\".")] string buildingId)
    {
        var building = repository.GetById(buildingId);
        if (building is null)
            return UnknownBuilding(buildingId, repository);

        var report = coffeeMachines.Get(building.Id);
        if (report is null)
        {
            var unknown = new JsonObject { ["known"] = false, ["buildingId"] = building.Id };
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = unknown.ToJsonString(CampusTools.JsonOptions) }],
                StructuredContent = JsonSerializer.SerializeToElement(unknown, CampusTools.JsonOptions),
            };
        }

        return CoffeeMachineResult(building, report, $"Znane miejsce ekspresu do kawy: {building.Name}.");
    }

    [McpServerResource(UriTemplate = MapViewUri, Name = "building-map", Title = "Widok mapy budynku", MimeType = McpApps.HtmlMimeType)]
    [Description("Widok MCP App rysujący GeoJSON budynku jako SVG. Wyświetlany przez host po wywołaniu show_building_map.")]
    public static string BuildingMapView() => MapViewHtml.Value;

    private static CallToolResult CoffeeMachineResult(CampusBuilding building, CoffeeMachineReport report, string message)
    {
        var point = new Point2(report.X, report.Y);
        var nearest = building.Entrances.MinBy(e => Geometry.Distance(point, e.Points[0]))!;

        var result = new JsonObject
        {
            ["known"] = true,
            ["buildingId"] = building.Id,
            ["name"] = building.Name,
            ["x"] = report.X,
            ["y"] = report.Y,
            ["insideBuilding"] = Geometry.IsInside(point, building.Outline),
            ["nearestEntranceId"] = nearest.Id,
            ["nearestEntranceDistanceM"] = Math.Round(Geometry.Distance(point, nearest.Points[0]), 1),
            ["message"] = message,
        };

        return new CallToolResult
        {
            Content = [new TextContentBlock { Text = result.ToJsonString(CampusTools.JsonOptions) }],
            StructuredContent = JsonSerializer.SerializeToElement(result, CampusTools.JsonOptions),
        };
    }

    private static JsonObject? CoffeeMachineJson(CoffeeMachineReport? report) =>
        report is null ? null : new JsonObject { ["x"] = report.X, ["y"] = report.Y };

    private static CallToolResult UnknownBuilding(string buildingId, BuildingRepository repository) =>
        Error($"Budynek o identyfikatorze \"{buildingId}\" nie istnieje. Dostępne budynki: {string.Join(", ", repository.All.Select(b => b.Id))}.");

    private static CallToolResult Error(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = JsonSerializer.Serialize(new ToolError(message), CampusTools.JsonOptions) }],
    };
}
