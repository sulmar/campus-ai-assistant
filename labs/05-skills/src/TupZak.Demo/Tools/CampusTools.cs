using System.ComponentModel;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.AI;
using TupZak.Demo.Services;

namespace TupZak.Demo.Tools;

/// <summary>
/// Narzędzia udostępniane modelowi. To zwykłe metody C#: model widzi tylko ich
/// nazwy, opisy i JSON Schema argumentów, a wykonuje je wyłącznie aplikacja.
/// </summary>
public sealed class CampusTools(BuildingRepository repository, MapState mapState)
{
    public const string FindBuildingName = "find_building";
    public const string GetBuildingGeometryName = "get_building_geometry";
    public const string MarkEntranceName = "mark_entrance";

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
    };

    /// <summary>Tool Definitions: nazwa + opis + JSON Schema wygenerowana z sygnatury metody.</summary>
    public IReadOnlyList<AIFunction> CreateAIFunctions() =>
    [
        AIFunctionFactory.Create(FindBuilding, FindBuildingName,
            "Wyszukuje budynek kampusu po nazwie lub identyfikatorze. Zwraca buildingId potrzebny w pozostałych narzędziach.",
            JsonOptions),
        AIFunctionFactory.Create(GetBuildingGeometry, GetBuildingGeometryName,
            "Zwraca geometrię budynku: obrys, wejścia ze współrzędnymi i elewacją, parkingi, ścieżki i punkty orientacyjne " +
            "wraz z kierunkami i odległościami. Współrzędne są w metrach: x rośnie na wschód, y na północ.",
            JsonOptions),
        AIFunctionFactory.Create(MarkEntrance, MarkEntranceName,
            "Zaznacza na mapie istniejące wejście do budynku. Przyjmuje wyłącznie identyfikatory zwrócone przez get_building_geometry.",
            JsonOptions),
    ];

    public FindBuildingResult FindBuilding(
        [Description("Nazwa lub identyfikator budynku, np. \"A\" albo \"Budynek A\".")] string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return FindBuildingResult.NotFound("Argument query nie może być pusty.", Summaries());

        var building = repository.Find(query);
        return building is null
            ? FindBuildingResult.NotFound($"Nie znaleziono budynku pasującego do zapytania \"{query}\".", Summaries())
            : new FindBuildingResult(true, building.Id, building.Name, null, null);
    }

    public BuildingGeometryResult GetBuildingGeometry(
        [Description("Identyfikator budynku zwrócony przez find_building, np. \"A\".")] string buildingId)
    {
        var building = repository.GetById(buildingId);
        if (building is null)
            return BuildingGeometryResult.Failure($"Budynek o identyfikatorze \"{buildingId}\" nie istnieje.");

        mapState.Focus(building.Id);

        var outline = building.Outline;
        var centroid = Geometry.Centroid(outline);

        var surroundings = building.Features.Select(f => Describe(f, building, centroid)).ToList();

        var entrances = building.Entrances.Select(e =>
        {
            var p = e.Points[0];
            var nearby = building.Features
                .Select(f => new NearbyFeature(f.Id, f.Kind, f.Name, Math.Round(DistanceTo(p, f), 1)))
                .OrderBy(n => n.DistanceM)
                .ToList();
            return new EntranceInfo(e.Id, e.Name, p.ToArray(), Geometry.FacadeDirection(p, outline), nearby);
        }).ToList();

        return new BuildingGeometryResult(
            Found: true,
            Error: null,
            BuildingId: building.Id,
            Name: building.Name,
            CoordinateSystem: "Lokalny układ współrzędnych w metrach: x rośnie na wschód, y rośnie na północ.",
            Outline: new OutlineInfo(
                "Polygon",
                outline.Select(p => p.ToArray()).ToList(),
                centroid.ToArray(),
                new BoundingBox(outline.Min(p => p.X), outline.Min(p => p.Y), outline.Max(p => p.X), outline.Max(p => p.Y))),
            Entrances: entrances,
            Parkings: surroundings.Where(s => s.Kind == "parking").ToList(),
            Paths: surroundings.Where(s => s.Kind == "path").ToList(),
            Landmarks: surroundings.Where(s => s.Kind is "landmark" or "courtyard").ToList());
    }

    public MarkEntranceResult MarkEntrance(
        [Description("Identyfikator budynku, np. \"A\".")] string buildingId,
        [Description("Identyfikator istniejącego wejścia tego budynku, np. \"A1\".")] string entranceId)
    {
        var building = repository.GetById(buildingId);
        if (building is null)
            return MarkEntranceResult.Failure(
                $"Budynek o identyfikatorze \"{buildingId}\" nie istnieje. Dostępne budynki: {string.Join(", ", repository.All.Select(b => b.Id))}.");

        var entrance = building.Entrances.FirstOrDefault(e => e.Id.Equals(entranceId?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (entrance is null)
        {
            var owner = repository.All.FirstOrDefault(b => b.Entrances.Any(e => e.Id.Equals(entranceId?.Trim(), StringComparison.OrdinalIgnoreCase)));
            string reason = owner is null
                ? $"Wejście \"{entranceId}\" nie istnieje."
                : $"Wejście \"{entranceId}\" należy do budynku {owner.Id}, a nie do budynku {building.Id}.";
            return MarkEntranceResult.Failure(reason, building.Entrances.Select(e => e.Id).ToList());
        }

        mapState.MarkEntrance(building.Id, entrance.Id);
        return new MarkEntranceResult(true, building.Id, entrance.Id, $"Zaznaczono {entrance.Name} budynku {building.Id} na mapie.", null, null);
    }

    private IReadOnlyList<BuildingSummary> Summaries() =>
        repository.All.Select(b => new BuildingSummary(b.Id, b.Name)).ToList();

    private SurroundingInfo Describe(MapFeature feature, CampusBuilding building, Point2 buildingCentroid)
    {
        var center = feature.GeometryType == "Polygon" ? Geometry.Centroid(feature.Points) : Geometry.Average(feature.Points);

        double distanceToBuilding = feature.Points.Min(p => Geometry.DistanceToPolygon(p, building.Outline));

        List<string>? connectedEntrances = null;
        if (feature.Kind == "path")
        {
            connectedEntrances = building.Entrances
                .Where(e => Geometry.Distance(e.Points[0], feature.Points[0]) < 1 || Geometry.Distance(e.Points[0], feature.Points[^1]) < 1)
                .Select(e => e.Id)
                .ToList();
        }

        return new SurroundingInfo(
            feature.Id,
            feature.Kind,
            feature.Name,
            feature.GeometryType,
            feature.Points.Select(p => p.ToArray()).ToList(),
            center.ToArray(),
            Geometry.Direction(buildingCentroid, center),
            Math.Round(distanceToBuilding, 1),
            connectedEntrances);
    }

    private static double DistanceTo(Point2 point, MapFeature feature) => feature.GeometryType switch
    {
        "Polygon" => Geometry.DistanceToPolygon(point, feature.Points),
        _ => Geometry.DistanceToPolyline(point, feature.Points),
    };
}

public sealed record BuildingSummary(string BuildingId, string Name);

public sealed record FindBuildingResult(
    bool Found,
    string? BuildingId,
    string? Name,
    string? Error,
    IReadOnlyList<BuildingSummary>? AvailableBuildings)
{
    public static FindBuildingResult NotFound(string error, IReadOnlyList<BuildingSummary> available) =>
        new(false, null, null, error, available);
}

public sealed record BoundingBox(double MinX, double MinY, double MaxX, double MaxY);

public sealed record OutlineInfo(string Type, IReadOnlyList<double[]> Coordinates, double[] Centroid, BoundingBox BoundingBox);

public sealed record EntranceInfo(
    string Id,
    string Name,
    double[] Coordinates,
    string Facade,
    IReadOnlyList<NearbyFeature> Nearby);

public sealed record NearbyFeature(string Id, string Kind, string Name, double DistanceM);

public sealed record SurroundingInfo(
    string Id,
    string Kind,
    string Name,
    string GeometryType,
    IReadOnlyList<double[]> Coordinates,
    double[] Center,
    string DirectionFromBuilding,
    double DistanceToBuildingM,
    IReadOnlyList<string>? ConnectedEntrances);

public sealed record BuildingGeometryResult(
    bool Found,
    string? Error,
    string? BuildingId,
    string? Name,
    string? CoordinateSystem,
    OutlineInfo? Outline,
    IReadOnlyList<EntranceInfo>? Entrances,
    IReadOnlyList<SurroundingInfo>? Parkings,
    IReadOnlyList<SurroundingInfo>? Paths,
    IReadOnlyList<SurroundingInfo>? Landmarks)
{
    public static BuildingGeometryResult Failure(string error) =>
        new(false, error, null, null, null, null, null, null, null, null);
}

public sealed record MarkEntranceResult(
    bool Success,
    string? BuildingId,
    string? EntranceId,
    string? Message,
    string? Error,
    IReadOnlyList<string>? AvailableEntrances)
{
    public static MarkEntranceResult Failure(string error, IReadOnlyList<string>? availableEntrances = null) =>
        new(false, null, null, null, error, availableEntrances);
}
