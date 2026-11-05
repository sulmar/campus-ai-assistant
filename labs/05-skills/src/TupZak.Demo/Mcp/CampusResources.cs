using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using TupZak.Demo.Services;
using TupZak.Demo.Tools;

namespace TupZak.Demo.Mcp;

/// <summary>
/// Zasoby MCP: dane tylko do odczytu, które host może pobrać (resources/read) bez wywoływania narzędzi.
/// W przeciwieństwie do narzędzi to host lub użytkownik decyduje, kiedy dołączyć zasób do kontekstu
/// (w Claude Code np. przez @tupzak:tupzak://buildings).
/// </summary>
[McpServerResourceType]
public sealed class CampusResources
{
    public const string BuildingsUri = "tupzak://buildings";
    public const string BuildingUriTemplate = "tupzak://buildings/{buildingId}";
    public const string PathsUri = "tupzak://paths";

    [McpServerResource(UriTemplate = BuildingsUri, Name = "buildings", Title = "Budynki kampusu", MimeType = "application/json")]
    [Description("Pełna lista budynków kampusu Tup Żak: identyfikator, nazwa i identyfikatory wejść.")]
    public static string Buildings(BuildingRepository repository) =>
        JsonSerializer.Serialize(
            repository.All.Select(b => new BuildingListItem(b.Id, b.Name, b.Entrances.Select(e => e.Id).ToList())),
            CampusTools.JsonOptions);

    [McpServerResource(UriTemplate = BuildingUriTemplate, Name = "building", Title = "Dane GeoJSON budynku", MimeType = "application/geo+json")]
    [Description("Źródłowe dane GeoJSON budynku wraz z otoczeniem. Współrzędne w metrach: x rośnie na wschód, y na północ.")]
    public static string Building(BuildingRepository repository, string buildingId) =>
        repository.GetById(buildingId)?.GeoJson.ToJsonString(CampusTools.JsonOptions)
        ?? throw new McpProtocolException(
            $"Budynek o identyfikatorze \"{buildingId}\" nie istnieje. Dostępne budynki: {string.Join(", ", repository.All.Select(b => b.Id))}.",
            McpErrorCode.ResourceNotFound);

    [McpServerResource(UriTemplate = PathsUri, Name = "paths", Title = "Aleje kampusu", MimeType = "application/json")]
    [Description("Aleje łączące ścieżki przy budynkach w jedną sieć: identyfikator, nazwa, dostępność dla wózka, zamknięcie i przebieg.")]
    public static string Paths(BuildingRepository repository, CampusNetwork network) =>
        JsonSerializer.Serialize(
            network.Paths.Values
                .Where(p => !repository.All.Any(b => b.Features.Any(f => f.Id == p.Id)))
                .Select(p => new { p.Id, p.Name, p.Accessible, p.Closed, Coordinates = p.Points.Select(x => x.ToArray()) }),
            CampusTools.JsonOptions);
}

public sealed record BuildingListItem(string BuildingId, string Name, IReadOnlyList<string> Entrances);
