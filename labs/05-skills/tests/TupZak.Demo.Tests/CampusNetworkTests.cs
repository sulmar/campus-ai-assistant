using TupZak.Demo.Services;

namespace TupZak.Demo.Tests;

/// <summary>Sieć ścieżek prawdziwego kampusu demonstracyjnego (labs/05-skills/data).</summary>
public class CampusNetworkTests
{
    private readonly (BuildingRepository Buildings, CampusNetwork Network, DeliveryPlanner Planner) _campus =
        TestData.LoadCampus(TestData.DataDirectory);

    [Fact]
    public void Every_entrance_parking_and_landmark_is_connected_to_one_network()
    {
        var network = _campus.Network;
        var start = network.GetPoint("A-P1")!;
        var (distance, _) = network.ShortestPaths(start.Nodes, PathConstraints.Default);

        Assert.All(network.Points, p =>
        {
            Assert.NotEmpty(p.Nodes);
            Assert.True(p.Nodes.Any(n => !double.IsPositiveInfinity(distance[n])), $"{p.Id} nie jest osiągalny z A-P1");
        });
    }

    [Fact]
    public void Demo_scenario_plans_route_through_all_three_buildings()
    {
        var plan = _campus.Planner.Optimize(new DeliveryPlanRequest("A-P1", [new Delivery("A"), new Delivery("B"), new Delivery("C")]));

        Assert.True(plan.Success, plan.Error);
        Assert.Equal(3, plan.Stops!.Count);
        Assert.Equal(["A", "B", "C"], plan.Stops.Select(s => s.BuildingId).Order());
        Assert.Equal(plan.TotalDistanceM!.Value, plan.Legs!.Sum(l => l.DistanceM), 1);
    }

    [Fact]
    public void Parking_P1_is_ambiguous_on_the_demo_campus()
    {
        var (point, error) = _campus.Planner.ResolvePoint("P1");

        Assert.Null(point);
        Assert.Contains("A-P1", error);
        Assert.Contains("B-P1", error);
        Assert.Contains("C-P1", error);
    }

    [Fact]
    public void Entrance_A3_is_reachable_only_by_stairs()
    {
        var withoutCart = _campus.Planner.Optimize(new DeliveryPlanRequest("A-P1", [new Delivery("A", "A3")]));
        var withCart = _campus.Planner.Optimize(new DeliveryPlanRequest("A-P1", [new Delivery("A", "A3", RequiresCart: true)]));

        Assert.True(withoutCart.Success, withoutCart.Error);
        Assert.Contains("W-04", withoutCart.Legs![0].PathIds);
        Assert.False(withCart.Success);
    }
}
