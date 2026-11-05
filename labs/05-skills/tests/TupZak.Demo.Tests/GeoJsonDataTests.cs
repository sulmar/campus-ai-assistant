using System.Text.Json.Nodes;
using TupZak.Demo.Services;

namespace TupZak.Demo.Tests;

public class GeoJsonDataTests
{
    [Fact]
    public void All_three_files_are_valid_and_loaded()
    {
        var repository = TestData.LoadRepository();

        Assert.Equal(["A", "B", "C"], repository.All.Select(b => b.Id));
        Assert.All(repository.All, b => Assert.True(b.Entrances.Count >= 3));
        Assert.All(repository.All, b => Assert.Contains(b.Features, f => f.Kind == "parking"));
        Assert.All(repository.All, b => Assert.Contains(b.Features, f => f.Kind == "path"));
    }

    [Fact]
    public void Building_A_is_a_rectangle_with_three_entrances_and_parking_on_the_west()
    {
        var a = TestData.LoadRepository().GetById("A")!;

        Assert.Equal(5, a.Outline.Count); // 4 wierzchołki + punkt zamykający
        Assert.Equal(["A1", "A2", "A3"], a.Entrances.Select(e => e.Id));

        var parking = Assert.Single(a.Features, f => f.Kind == "parking");
        Assert.True(parking.Points.Max(p => p.X) < a.Outline.Min(p => p.X));
    }

    [Fact]
    public void Building_B_is_U_shaped_with_courtyard_and_C_is_L_shaped()
    {
        var repository = TestData.LoadRepository();
        var b = repository.GetById("B")!;
        var c = repository.GetById("C")!;

        Assert.Equal(9, b.Outline.Count); // 8 wierzchołków kształtu U
        Assert.Contains(b.Features, f => f.Kind == "courtyard");
        Assert.Equal(7, c.Outline.Count); // 6 wierzchołków kształtu L
        Assert.True(c.Entrances.Count >= 3);
    }

    [Theory]
    [InlineData("building-a.geojson")]
    [InlineData("building-b.geojson")]
    [InlineData("building-c.geojson")]
    public void Data_does_not_contain_ready_answers(string file)
    {
        string json = File.ReadAllText(Path.Combine(TestData.DataDirectory, file));
        string[] forbidden = ["correctEntrance", "entranceFromParking", "expectedResult", "answer"];

        var propertyNames = JsonNode.Parse(json)!["features"]!.AsArray()
            .SelectMany(f => f!["properties"]!.AsObject().Select(p => p.Key));

        Assert.DoesNotContain(propertyNames, name => forbidden.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("building-a.geojson")]
    [InlineData("building-b.geojson")]
    [InlineData("building-c.geojson")]
    public void Polygons_follow_RFC_7946_counterclockwise_exterior_rings(string file)
    {
        var building = BuildingRepository.Parse(File.ReadAllText(Path.Combine(TestData.DataDirectory, file)), file);

        Assert.True(Geometry.SignedArea(building.Outline) > 0);
        Assert.All(building.Features.Where(f => f.GeometryType == "Polygon"), f => Assert.True(Geometry.SignedArea(f.Points) > 0));
    }

    [Fact]
    public void Unclosed_ring_is_rejected()
    {
        string json = Collection(Building("[[0,0],[10,0],[10,10],[0,10]]"), Entrance("X1", "X", 5, 0));
        var ex = Assert.Throws<InvalidDataException>(() => BuildingRepository.Parse(json, "test"));
        Assert.Contains("nie jest zamknięty", ex.Message);
    }

    [Fact]
    public void Entrance_outside_the_outline_is_rejected()
    {
        string json = Collection(Building("[[0,0],[10,0],[10,10],[0,10],[0,0]]"), Entrance("X1", "X", 5, 5));
        var ex = Assert.Throws<InvalidDataException>(() => BuildingRepository.Parse(json, "test"));
        Assert.Contains("nie leży na obrysie", ex.Message);
    }

    [Fact]
    public void Entrance_pointing_to_another_building_is_rejected()
    {
        string json = Collection(Building("[[0,0],[10,0],[10,10],[0,10],[0,0]]"), Entrance("X1", "Y", 5, 0));
        var ex = Assert.Throws<InvalidDataException>(() => BuildingRepository.Parse(json, "test"));
        Assert.Contains("nie wskazuje budynku", ex.Message);
    }

    [Fact]
    public void Duplicate_identifiers_are_rejected()
    {
        string json = Collection(Building("[[0,0],[10,0],[10,10],[0,10],[0,0]]"), Entrance("X1", "X", 5, 0), Entrance("X1", "X", 10, 5));
        var ex = Assert.Throws<InvalidDataException>(() => BuildingRepository.Parse(json, "test"));
        Assert.Contains("wielokrotnie", ex.Message);
    }

    [Fact]
    public void Wrong_geometry_type_for_kind_is_rejected()
    {
        string json = Collection(
            Building("[[0,0],[10,0],[10,10],[0,10],[0,0]]"),
            """{ "type": "Feature", "properties": { "id": "X1", "kind": "entrance", "buildingId": "X" }, "geometry": { "type": "LineString", "coordinates": [[0,0],[1,0]] } }""");
        var ex = Assert.Throws<InvalidDataException>(() => BuildingRepository.Parse(json, "test"));
        Assert.Contains("powinien mieć geometrię Point", ex.Message);
    }

    private static string Building(string ring) =>
        $$"""{ "type": "Feature", "properties": { "id": "X", "kind": "building", "name": "Budynek X" }, "geometry": { "type": "Polygon", "coordinates": [{{ring}}] } }""";

    private static string Entrance(string id, string buildingId, double x, double y) =>
        $$"""{ "type": "Feature", "properties": { "id": "{{id}}", "kind": "entrance", "buildingId": "{{buildingId}}" }, "geometry": { "type": "Point", "coordinates": [{{x}}, {{y}}] } }""";

    private static string Collection(params string[] features) =>
        $$"""{ "type": "FeatureCollection", "features": [{{string.Join(",", features)}}] }""";
}
