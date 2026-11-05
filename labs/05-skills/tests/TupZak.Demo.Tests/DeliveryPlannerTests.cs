using TupZak.Demo.Services;

namespace TupZak.Demo.Tests;

/// <summary>
/// Planowanie dostaw na małym kampusie testowym (TestCampus). Wszystkie odległości da się policzyć w pamięci:
///
///   K-P1 ─W-00─ (0,0) ─W-01─ (100,0) ─W-02 schody─ (200,0) ─W-03─ (280,0) ── (300,0) ─W-04─ M-P1
///                              │ K-S1               │ L-S1        │ M-S2       │ M-S1    ╲ W-06 (zamknięta) → N
///                              K1                   L1            M2           M1
///                              └──────── W-05 objazd bez schodów (200 m) ──┘
///
///   K-P1→K1 = 10 + 100 + 10 = 120     K1→L1 = 120 (z wózkiem 220)     L1→M2 = 10 + 80 + 15 + 10 = 115
///   L1→M1 = 120     K-P1→M2 = 315     K-P1→M1 = 320     M-P1→M1 = 20     M-P1→M2 = 55
/// </summary>
public class DeliveryPlannerTests
{
    private readonly DeliveryPlanner _planner = TestData.LoadCampus(TestData.TestCampusDirectory).Planner;

    private DeliveryPlan Optimize(string start, bool returnToStart, params Delivery[] deliveries) =>
        _planner.Optimize(new DeliveryPlanRequest(start, deliveries, returnToStart));

    private static string[] Entrances(DeliveryPlan plan) => plan.Stops!.Select(s => s.EntranceId).ToArray();

    [Fact]
    public void Single_delivery()
    {
        var plan = Optimize("K-P1", false, new Delivery("L"));

        Assert.True(plan.Success, plan.Error);
        Assert.Equal(["L1"], Entrances(plan));
        Assert.Equal(220, plan.TotalDistanceM);
        var leg = Assert.Single(plan.Legs!);
        Assert.Equal(["W-00", "W-01", "W-02", "L-S1"], leg.PathIds);
    }

    [Fact]
    public void Three_deliveries_given_in_wrong_order_are_reordered()
    {
        var plan = Optimize("K-P1", false, new Delivery("M"), new Delivery("K"), new Delivery("L"));

        Assert.True(plan.Success, plan.Error);
        Assert.Equal(["K1", "L1", "M2"], Entrances(plan));
        Assert.Equal([120, 120, 115], plan.Legs!.Select(l => l.DistanceM));
        Assert.Equal(355, plan.TotalDistanceM);
        Assert.Equal(6, plan.EvaluatedOrders);
    }

    [Fact]
    public void Different_start_changes_order_and_entrance()
    {
        var plan = Optimize("M-P1", false, new Delivery("M"), new Delivery("K"), new Delivery("L"));

        Assert.Equal(["M1", "L1", "K1"], Entrances(plan));
        Assert.Equal(260, plan.TotalDistanceM);   // 20 + 120 + 120
    }

    [Fact]
    public void Return_to_start_adds_final_leg()
    {
        var plan = Optimize("K-P1", true, new Delivery("M"), new Delivery("K"), new Delivery("L"));

        Assert.Equal(670, plan.TotalDistanceM);   // 120 + 120 + 115 + 315 (M2 → K-P1)
        Assert.Equal(4, plan.Legs!.Count);
        Assert.Equal("K-P1", plan.Legs[^1].To);
    }

    [Fact]
    public void Avoided_path_makes_other_entrance_the_best_one()
    {
        var plan = _planner.Optimize(new DeliveryPlanRequest("K-P1", [new Delivery("M")], AvoidPathIds: ["M-S2"]));

        Assert.Equal(["M1"], Entrances(plan));
        Assert.Equal(320, plan.TotalDistanceM);
    }

    [Fact]
    public void Required_entrance_behind_avoided_path_is_unreachable()
    {
        var plan = _planner.Optimize(new DeliveryPlanRequest("K-P1", [new Delivery("M", "M2")], AvoidPathIds: ["M-S2"]));

        Assert.False(plan.Success);
        Assert.Equal(["M2"], plan.Unreachable);
    }

    [Fact]
    public void Cart_avoids_stairs_using_a_detour()
    {
        var plan = Optimize("K-P1", false, new Delivery("K"), new Delivery("L", RequiresCart: true));

        Assert.True(plan.AccessibleRouteOnly);
        Assert.Equal(340, plan.TotalDistanceM);   // 120 + 220 (objazd W-05 zamiast schodów W-02)
        Assert.Contains("W-05", plan.Legs![1].PathIds);
        Assert.DoesNotContain(plan.Legs, l => l.PathIds.Contains("W-02"));
    }

    [Fact]
    public void Building_without_connection_is_reported()
    {
        var plan = Optimize("K-P1", false, new Delivery("K"), new Delivery("N"));

        Assert.False(plan.Success);
        Assert.Equal(["N"], plan.Unreachable);   // jedyna ścieżka do N (W-06) jest zamknięta
    }

    [Fact]
    public void Building_with_two_entrances_uses_the_closer_one()
    {
        Assert.Equal(["M2"], Entrances(Optimize("K-P1", false, new Delivery("M"))));
        Assert.Equal(["M1"], Entrances(Optimize("M-P1", false, new Delivery("M"))));
    }

    [Fact]
    public void Fixed_order_is_evaluated_without_optimization()
    {
        var plan = _planner.Evaluate("K-P1", ["M1", "K1", "L1"], returnToStart: false, requiresCart: false, avoidPathIds: null);

        Assert.True(plan.Success, plan.Error);
        Assert.Equal(660, plan.TotalDistanceM);   // 320 + 220 + 120 — gorzej niż optymalne 355
        Assert.Null(plan.EvaluatedOrders);
    }

    [Fact]
    public void Ambiguous_start_lists_candidates()
    {
        var plan = Optimize("P1", false, new Delivery("K"));

        Assert.False(plan.Success);
        Assert.Contains("K-P1", plan.Error);
        Assert.Contains("M-P1", plan.Error);
    }

    [Fact]
    public void Distance_matrix_expands_building_to_entrances()
    {
        var matrix = _planner.Matrix(["K-P1", "M"], requiresCart: false, avoidPathIds: null);

        Assert.Equal(["K-P1", "M1", "M2"], matrix.Points!.Select(p => p.Id));
        Assert.Contains(matrix.Distances!, d => d is { From: "K-P1", To: "M2", DistanceM: 315 });
        Assert.Contains(matrix.Distances!, d => d is { From: "M1", To: "M2", DistanceM: 55 });
    }

    [Fact]
    public void Unknown_avoided_path_is_rejected()
    {
        var plan = _planner.Optimize(new DeliveryPlanRequest("K-P1", [new Delivery("K")], AvoidPathIds: ["X-99"]));

        Assert.False(plan.Success);
        Assert.Contains("X-99", plan.Error);
    }
}
