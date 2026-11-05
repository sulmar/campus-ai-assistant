using TupZak.Demo.Services;

namespace TupZak.Demo.Tests;

/// <summary>Optymalizator działa na samej macierzy kosztów — bez grafu, budynków i mapy.</summary>
public class DeliveryOrderOptimizerTests
{
    private const double Inf = double.PositiveInfinity;

    /// <summary>Odległości z przykładowego skryptu Pythona: indeksy 0 = P1, 1 = A, 2 = B, 3 = C.</summary>
    private static readonly double[,] ScriptExample =
    {
        //  P1    A    B    C
        {   0, 300, 100, 500 },
        { 300,   0, 250, 150 },
        { 100, 250,   0, 350 },
        { 500, 150, 350,   0 },
    };

    private static IReadOnlyList<IReadOnlyList<int>> Single(params int[] locations) =>
        locations.Select(l => (IReadOnlyList<int>)[l]).ToList();

    [Fact]
    public void Matches_the_python_script_example()
    {
        var solution = DeliveryOrderOptimizer.Solve(ScriptExample, 0, Single(1, 2, 3), returnToStart: false)!;

        Assert.Equal([2, 1, 3], solution.Chosen);   // P1 → B → A → C
        Assert.Equal(500, solution.Total);
        Assert.Equal(6, solution.EvaluatedOrders);
    }

    [Fact]
    public void Return_to_start_is_added_to_the_cost()
    {
        var solution = DeliveryOrderOptimizer.Solve(ScriptExample, 0, Single(1, 2, 3), returnToStart: true)!;

        // P1 → B → A → C → P1 = 100 + 250 + 150 + 500 = 1000 i P1 → A → C → B → P1 = 300 + 150 + 350 + 100 = 900.
        Assert.Equal(900, solution.Total);
    }

    [Fact]
    public void Best_candidate_of_a_delivery_depends_on_neighbouring_stops()
    {
        // 0 = start, dostawa X ma dwa wejścia: 1 (blisko startu) i 2 (blisko dostawy Y = 3).
        double[,] costs =
        {
            {  0, 10, 30, 100 },
            { 10,  0,  5,  90 },
            { 30,  5,  0,   5 },
            {100, 90,  5,   0 },
        };

        var solution = DeliveryOrderOptimizer.Solve(costs, 0, [[1, 2], [3]], returnToStart: false)!;

        Assert.Equal([2, 3], solution.Chosen);   // 30 + 5 = 35 jest lepsze niż 10 + 90 = 100
        Assert.Equal(35, solution.Total);
    }

    [Fact]
    public void Unreachable_location_gives_no_solution()
    {
        double[,] costs = { { 0, 10, Inf }, { 10, 0, Inf }, { Inf, Inf, 0 } };

        Assert.Null(DeliveryOrderOptimizer.Solve(costs, 0, Single(1, 2), returnToStart: false));
    }

    [Fact]
    public void Permutations_are_complete_and_lexicographic()
    {
        var permutations = DeliveryOrderOptimizer.Permutations(3).Select(p => string.Concat(p)).ToList();

        Assert.Equal(["012", "021", "102", "120", "201", "210"], permutations);
    }
}
