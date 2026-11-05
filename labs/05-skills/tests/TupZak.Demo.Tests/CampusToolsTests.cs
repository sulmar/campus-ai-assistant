using TupZak.Demo.Services;
using TupZak.Demo.Tools;

namespace TupZak.Demo.Tests;

public class CampusToolsTests
{
    private readonly MapState _mapState = new();
    private readonly CampusTools _tools;

    public CampusToolsTests()
    {
        _tools = new CampusTools(TestData.LoadRepository(), _mapState);
    }

    // ---------- find_building ----------

    [Theory]
    [InlineData("A")]
    [InlineData("a")]
    [InlineData("Budynek A")]
    [InlineData("budynku A")]
    [InlineData("  budynek a. ")]
    public void FindBuilding_finds_building_by_id_or_name(string query)
    {
        var result = _tools.FindBuilding(query);

        Assert.True(result.Found);
        Assert.Equal("A", result.BuildingId);
        Assert.Equal("Budynek A", result.Name);
        Assert.Null(result.Error);
    }

    [Fact]
    public void FindBuilding_reports_missing_building_and_lists_available_ones()
    {
        var result = _tools.FindBuilding("D");

        Assert.False(result.Found);
        Assert.Null(result.BuildingId);
        Assert.Contains("Nie znaleziono", result.Error);
        Assert.Equal(["A", "B", "C"], result.AvailableBuildings!.Select(b => b.BuildingId));
    }

    [Fact]
    public void FindBuilding_rejects_empty_query()
    {
        var result = _tools.FindBuilding(" ");
        Assert.False(result.Found);
        Assert.Contains("pusty", result.Error);
    }

    // ---------- get_building_geometry ----------

    [Fact]
    public void GetBuildingGeometry_returns_spatial_data_for_reasoning()
    {
        var result = _tools.GetBuildingGeometry("A");

        Assert.True(result.Found);
        Assert.Equal(5, result.Outline!.Coordinates.Count);
        Assert.Equal(3, result.Entrances!.Count);

        var a1 = result.Entrances.Single(e => e.Id == "A1");
        Assert.Equal([0, 30], a1.Coordinates);
        Assert.Equal("zachód (W)", a1.Facade);
        Assert.Equal("A-P1", a1.Nearby.Where(n => n.Kind == "parking").MinBy(n => n.DistanceM)!.Id);

        var parking = Assert.Single(result.Parkings!);
        Assert.Equal("zachód (W)", parking.DirectionFromBuilding);
        Assert.Contains(result.Paths!, p => p.ConnectedEntrances!.Contains("A1"));
        Assert.NotEmpty(result.Landmarks!);
    }

    [Fact]
    public void GetBuildingGeometry_does_not_name_the_correct_entrance()
    {
        // Narzędzie zwraca fakty (współrzędne, kierunki, odległości), a nie gotową odpowiedź.
        string json = System.Text.Json.JsonSerializer.Serialize(_tools.GetBuildingGeometry("A"), CampusTools.JsonOptions);
        Assert.DoesNotContain("correct", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expected", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetBuildingGeometry_focuses_map_on_building()
    {
        _tools.GetBuildingGeometry("B");
        Assert.Equal("B", _mapState.Snapshot.FocusedBuildingId);
    }

    [Fact]
    public void GetBuildingGeometry_reports_unknown_building()
    {
        var result = _tools.GetBuildingGeometry("D");

        Assert.False(result.Found);
        Assert.Contains("nie istnieje", result.Error);
        Assert.Null(result.Entrances);
        Assert.Null(_mapState.Snapshot.FocusedBuildingId);
    }

    // ---------- mark_entrance ----------

    [Fact]
    public void MarkEntrance_marks_existing_entrance_and_updates_map()
    {
        var result = _tools.MarkEntrance("A", "A1");

        Assert.True(result.Success);
        Assert.Equal(new MapSnapshot("A", "A", "A1"), _mapState.Snapshot);
    }

    [Fact]
    public void MarkEntrance_is_case_insensitive_and_returns_canonical_ids()
    {
        var result = _tools.MarkEntrance("a", "a1");

        Assert.True(result.Success);
        Assert.Equal("A", result.BuildingId);
        Assert.Equal("A1", result.EntranceId);
    }

    [Fact]
    public void MarkEntrance_rejects_unknown_building()
    {
        var result = _tools.MarkEntrance("D", "D1");

        Assert.False(result.Success);
        Assert.Contains("nie istnieje", result.Error);
        Assert.Null(_mapState.Snapshot.MarkedEntranceId);
    }

    [Fact]
    public void MarkEntrance_rejects_entrance_of_another_building()
    {
        var result = _tools.MarkEntrance("A", "B1");

        Assert.False(result.Success);
        Assert.Contains("należy do budynku B", result.Error);
        Assert.Equal(["A1", "A2", "A3"], result.AvailableEntrances);
        Assert.Null(_mapState.Snapshot.MarkedEntranceId);
    }

    [Fact]
    public void MarkEntrance_rejects_nonexistent_entrance()
    {
        var result = _tools.MarkEntrance("A", "A9");

        Assert.False(result.Success);
        Assert.Contains("nie istnieje", result.Error);
        Assert.Equal(["A1", "A2", "A3"], result.AvailableEntrances);
    }

    [Fact]
    public void Tool_definitions_have_names_descriptions_and_required_arguments()
    {
        var functions = _tools.CreateAIFunctions();

        Assert.Equal(["find_building", "get_building_geometry", "mark_entrance"], functions.Select(f => f.Name));
        Assert.All(functions, f => Assert.False(string.IsNullOrWhiteSpace(f.Description)));

        var markEntrance = functions.Single(f => f.Name == "mark_entrance");
        var required = markEntrance.JsonSchema.GetProperty("required").EnumerateArray().Select(r => r.GetString());
        Assert.Equal(["buildingId", "entranceId"], required);
    }
}
