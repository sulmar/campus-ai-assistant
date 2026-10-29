using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TupZak.Demo.Services;

/// <summary>Obiekt z pliku GeoJSON: wejście, parking, ścieżka, dziedziniec lub punkt orientacyjny.</summary>
public sealed record MapFeature(string Id, string Kind, string Name, string? BuildingId, string GeometryType, IReadOnlyList<Point2> Points);

public sealed record CampusBuilding(
    string Id,
    string Name,
    IReadOnlyList<Point2> Outline,
    IReadOnlyList<MapFeature> Entrances,
    IReadOnlyList<MapFeature> Features,
    JsonNode GeoJson);

/// <summary>
/// Wczytuje budynki z plików GeoJSON (jeden plik = jeden budynek z otoczeniem)
/// i sprawdza poprawność danych już przy starcie aplikacji.
/// </summary>
public sealed partial class BuildingRepository
{
    private static readonly Dictionary<string, string> GeometryByKind = new()
    {
        ["building"] = "Polygon",
        ["entrance"] = "Point",
        ["parking"] = "Polygon",
        ["courtyard"] = "Polygon",
        ["path"] = "LineString",
        ["landmark"] = "Point",
    };

    private readonly List<CampusBuilding> _buildings;

    public BuildingRepository(IEnumerable<CampusBuilding> buildings)
    {
        _buildings = buildings.OrderBy(b => b.Id).ToList();
    }

    public IReadOnlyList<CampusBuilding> All => _buildings;

    public static BuildingRepository LoadFromDirectory(string directory)
    {
        var files = Directory.GetFiles(directory, "*.geojson").OrderBy(f => f).ToList();
        if (files.Count == 0)
            throw new InvalidDataException($"Brak plików *.geojson w katalogu {directory}.");

        var buildings = files.Select(f => Parse(File.ReadAllText(f), Path.GetFileName(f))).ToList();

        var duplicate = buildings.GroupBy(b => b.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"Budynek o identyfikatorze '{duplicate.Key}' występuje w kilku plikach.");

        return new BuildingRepository(buildings);
    }

    public CampusBuilding? GetById(string? buildingId) =>
        string.IsNullOrWhiteSpace(buildingId)
            ? null
            : _buildings.FirstOrDefault(b => b.Id.Equals(buildingId.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Szuka budynku po identyfikatorze lub nazwie, np. „A”, „budynek A”, „Budynek A”.</summary>
    public CampusBuilding? Find(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        string normalized = query.Trim().ToLowerInvariant();
        var byName = _buildings.FirstOrDefault(b => b.Name.Equals(normalized, StringComparison.OrdinalIgnoreCase));
        if (byName is not null) return byName;

        normalized = BuildingWords().Replace(normalized, " ");
        normalized = NonWordCharacters().Replace(normalized, " ").Trim();
        return GetById(normalized);
    }

    public static CampusBuilding Parse(string json, string sourceName)
    {
        JsonNode root;
        try
        {
            root = JsonNode.Parse(json) ?? throw new InvalidDataException("Pusty dokument.");
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new InvalidDataException($"{sourceName}: niepoprawny JSON ({ex.Message}).", ex);
        }

        if ((string?)root["type"] != "FeatureCollection")
            throw new InvalidDataException($"{sourceName}: oczekiwano obiektu typu FeatureCollection.");

        var features = root["features"]?.AsArray()
            ?? throw new InvalidDataException($"{sourceName}: brak tablicy features.");

        var parsed = new List<MapFeature>();
        foreach (var feature in features)
        {
            if (feature is null) throw new InvalidDataException($"{sourceName}: pusty obiekt Feature.");
            parsed.Add(ParseFeature(feature, sourceName));
        }

        var duplicateId = parsed.GroupBy(f => f.Id, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (duplicateId is not null)
            throw new InvalidDataException($"{sourceName}: identyfikator '{duplicateId.Key}' występuje wielokrotnie.");

        var buildingFeatures = parsed.Where(f => f.Kind == "building").ToList();
        if (buildingFeatures.Count != 1)
            throw new InvalidDataException($"{sourceName}: plik musi zawierać dokładnie jeden obiekt kind=building.");
        var building = buildingFeatures[0];

        var entrances = parsed.Where(f => f.Kind == "entrance").ToList();
        if (entrances.Count == 0)
            throw new InvalidDataException($"{sourceName}: budynek {building.Id} nie ma żadnego wejścia.");

        foreach (var entrance in entrances)
        {
            if (!string.Equals(entrance.BuildingId, building.Id, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"{sourceName}: wejście {entrance.Id} nie wskazuje budynku {building.Id} (buildingId='{entrance.BuildingId}').");

            if (Geometry.DistanceToPolyline(entrance.Points[0], building.Points) > 0.5)
                throw new InvalidDataException($"{sourceName}: wejście {entrance.Id} nie leży na obrysie budynku {building.Id}.");
        }

        return new CampusBuilding(
            building.Id,
            building.Name,
            building.Points,
            entrances,
            parsed.Where(f => f.Kind is not ("building" or "entrance")).ToList(),
            root);
    }

    private static MapFeature ParseFeature(JsonNode feature, string sourceName)
    {
        var properties = feature["properties"] ?? throw new InvalidDataException($"{sourceName}: obiekt Feature bez properties.");
        string id = (string?)properties["id"] ?? throw new InvalidDataException($"{sourceName}: obiekt Feature bez properties.id.");
        string kind = (string?)properties["kind"] ?? throw new InvalidDataException($"{sourceName}: {id} nie ma properties.kind.");
        string name = (string?)properties["name"] ?? id;
        string? buildingId = (string?)properties["buildingId"];

        if (!GeometryByKind.TryGetValue(kind, out var expectedGeometry))
            throw new InvalidDataException($"{sourceName}: {id} ma nieznany rodzaj '{kind}'.");

        var geometry = feature["geometry"] ?? throw new InvalidDataException($"{sourceName}: {id} nie ma geometrii.");
        string? geometryType = (string?)geometry["type"];
        if (geometryType != expectedGeometry)
            throw new InvalidDataException($"{sourceName}: {id} ({kind}) powinien mieć geometrię {expectedGeometry}, a ma {geometryType}.");

        var coordinates = geometry["coordinates"] ?? throw new InvalidDataException($"{sourceName}: {id} nie ma współrzędnych.");
        var points = geometryType switch
        {
            "Point" => [ParsePosition(coordinates, id, sourceName)],
            "LineString" => coordinates.AsArray().Select(c => ParsePosition(c!, id, sourceName)).ToList(),
            _ => ParseRing(coordinates, id, sourceName),
        };

        if (geometryType == "LineString" && points.Count < 2)
            throw new InvalidDataException($"{sourceName}: linia {id} musi mieć co najmniej dwa punkty.");

        return new MapFeature(id, kind, name, buildingId, geometryType, points);
    }

    private static List<Point2> ParseRing(JsonNode coordinates, string id, string sourceName)
    {
        var rings = coordinates.AsArray();
        if (rings.Count != 1)
            throw new InvalidDataException($"{sourceName}: wielokąt {id} powinien mieć dokładnie jeden pierścień (bez otworów).");

        var ring = rings[0]!.AsArray().Select(c => ParsePosition(c!, id, sourceName)).ToList();
        if (ring.Count < 4)
            throw new InvalidDataException($"{sourceName}: pierścień wielokąta {id} musi mieć co najmniej 4 punkty.");
        if (ring[0] != ring[^1])
            throw new InvalidDataException($"{sourceName}: pierścień wielokąta {id} nie jest zamknięty.");
        if (Math.Abs(Geometry.SignedArea(ring)) < 1e-9)
            throw new InvalidDataException($"{sourceName}: wielokąt {id} ma zerowe pole.");
        return ring;
    }

    private static Point2 ParsePosition(JsonNode position, string id, string sourceName)
    {
        var values = position.AsArray();
        if (values.Count != 2)
            throw new InvalidDataException($"{sourceName}: {id} zawiera pozycję, która nie ma dokładnie dwóch współrzędnych.");
        return new Point2(values[0]!.GetValue<double>(), values[1]!.GetValue<double>());
    }

    [GeneratedRegex(@"\b(budynek|budynku|budynkiem|bud\.|gmach|gmachu)\b", RegexOptions.IgnoreCase)]
    private static partial Regex BuildingWords();

    [GeneratedRegex(@"[^\p{L}\p{N}\-]+")]
    private static partial Regex NonWordCharacters();
}
