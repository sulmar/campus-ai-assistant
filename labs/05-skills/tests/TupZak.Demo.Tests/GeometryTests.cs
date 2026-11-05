using TupZak.Demo.Services;

namespace TupZak.Demo.Tests;

public class GeometryTests
{
    [Theory]
    [InlineData("A", "A1", "zachód (W)")]
    [InlineData("A", "A2", "południe (S)")]
    [InlineData("A", "A3", "wschód (E)")]
    [InlineData("B", "B2", "północ (N)")] // ściana od strony dziedzińca
    [InlineData("C", "C2", "wschód (E)")] // wewnętrzny narożnik kształtu L
    public void Facade_direction_is_computed_from_outline(string buildingId, string entranceId, string expected)
    {
        var building = TestData.LoadRepository().GetById(buildingId)!;
        var entrance = building.Entrances.Single(e => e.Id == entranceId);

        Assert.Equal(expected, Geometry.FacadeDirection(entrance.Points[0], building.Outline));
    }

    [Theory]
    [InlineData(1, 0, "wschód (E)")]
    [InlineData(0, 1, "północ (N)")]
    [InlineData(-1, 0, "zachód (W)")]
    [InlineData(0, -1, "południe (S)")]
    [InlineData(1, 1, "północny wschód (NE)")]
    [InlineData(-1, -1, "południowy zachód (SW)")]
    public void Compass_direction_maps_vectors(double dx, double dy, string expected) =>
        Assert.Equal(expected, Geometry.CompassDirection(dx, dy));

    [Fact]
    public void Distance_to_polygon_is_zero_inside_and_positive_outside()
    {
        Point2[] square = [new(0, 0), new(10, 0), new(10, 10), new(0, 10), new(0, 0)];

        Assert.Equal(0, Geometry.DistanceToPolygon(new Point2(5, 5), square));
        Assert.Equal(3, Geometry.DistanceToPolygon(new Point2(13, 5), square), 6);
    }

    [Fact]
    public void Centroid_of_rectangle_is_its_center()
    {
        Point2[] rectangle = [new(0, 0), new(24, 0), new(24, 60), new(0, 60), new(0, 0)];
        Assert.Equal(new Point2(12, 30), Geometry.Centroid(rectangle));
    }
}
