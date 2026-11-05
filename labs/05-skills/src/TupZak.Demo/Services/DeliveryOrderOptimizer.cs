namespace TupZak.Demo.Services;

/// <summary>Najlepsza kolejność: dla każdej dostawy (w kolejności odwiedzania) wybrany kandydat, np. wejście.</summary>
public sealed record OrderSolution(IReadOnlyList<int> Order, IReadOnlyList<int> Chosen, double Total, int EvaluatedOrders);

/// <summary>
/// Optymalizacja kolejności odwiedzania punktów — niezależna od grafu, budynków i mapy.
/// Dostaje tylko macierz kosztów, więc tę samą logikę można odtworzyć w skrypcie Skilla
/// albo w kodzie wygenerowanym przez agenta i porównać wyniki.
///
/// Algorytm dokładny:
///   1. przegląd wszystkich permutacji dostaw (n! kolejności, dla n ≤ 8),
///   2. dla danej kolejności wybór kandydatów (np. wejść) programowaniem dynamicznym
///      po kolejnych dostawach — każde wejście jest wybierane z uwzględnieniem poprzedniego i następnego etapu.
/// </summary>
public static class DeliveryOrderOptimizer
{
    public const int MaxDeliveries = 8;

    /// <param name="costs">Symetryczna macierz kosztów między lokalizacjami (∞ = brak trasy).</param>
    /// <param name="start">Indeks lokalizacji startowej w macierzy.</param>
    /// <param name="candidates">Dla każdej dostawy indeksy lokalizacji, które ją realizują (np. wejścia budynku).</param>
    /// <param name="returnToStart">Czy doliczyć powrót do lokalizacji startowej.</param>
    /// <returns>Najlepsze rozwiązanie albo null, gdy żadna kolejność nie ma skończonego kosztu.</returns>
    public static OrderSolution? Solve(double[,] costs, int start, IReadOnlyList<IReadOnlyList<int>> candidates, bool returnToStart)
    {
        if (candidates.Count > MaxDeliveries)
            throw new ArgumentException($"Przegląd permutacji obsługuje najwyżej {MaxDeliveries} dostaw.", nameof(candidates));

        OrderSolution? best = null;
        int evaluated = 0;

        foreach (var order in Permutations(candidates.Count))
        {
            evaluated++;
            var (total, chosen) = BestForOrder(costs, start, order.Select(i => candidates[i]).ToList(), returnToStart);
            // Ścisła nierówność: przy remisie zostaje pierwsza znaleziona kolejność (wynik deterministyczny).
            if (!double.IsPositiveInfinity(total) && (best is null || total < best.Total - 1e-9))
                best = new OrderSolution(order, chosen, total, 0);
        }

        return best is null ? null : best with { EvaluatedOrders = evaluated };
    }

    /// <summary>Koszt ustalonej kolejności z najlepszym wyborem kandydatów (programowanie dynamiczne po etapach).</summary>
    public static (double Total, IReadOnlyList<int> Chosen) BestForOrder(
        double[,] costs, int start, IReadOnlyList<IReadOnlyList<int>> layers, bool returnToStart)
    {
        if (layers.Count == 0) return (0, []);

        // cost[k][c] = najmniejszy koszt dojścia do kandydata c etapu k; from[k][c] = kandydat z etapu k-1.
        var cost = new double[layers.Count][];
        var from = new int[layers.Count][];
        cost[0] = layers[0].Select(c => costs[start, c]).ToArray();
        from[0] = new int[layers[0].Count];

        for (int k = 1; k < layers.Count; k++)
        {
            cost[k] = new double[layers[k].Count];
            from[k] = new int[layers[k].Count];
            for (int c = 0; c < layers[k].Count; c++)
            {
                cost[k][c] = double.PositiveInfinity;
                for (int p = 0; p < layers[k - 1].Count; p++)
                {
                    double candidate = cost[k - 1][p] + costs[layers[k - 1][p], layers[k][c]];
                    if (candidate < cost[k][c]) { cost[k][c] = candidate; from[k][c] = p; }
                }
            }
        }

        int last = layers.Count - 1;
        var totals = Enumerable.Range(0, layers[last].Count)
            .Select(c => cost[last][c] + (returnToStart ? costs[layers[last][c], start] : 0))
            .ToArray();
        int bestLast = Array.IndexOf(totals, totals.Min());
        if (double.IsPositiveInfinity(totals[bestLast])) return (double.PositiveInfinity, []);

        var chosen = new int[layers.Count];
        chosen[last] = bestLast;
        for (int k = last; k > 0; k--)
            chosen[k - 1] = from[k][chosen[k]];

        return (totals[bestLast], chosen.Select((c, k) => layers[k][c]).ToList());
    }

    /// <summary>Wszystkie permutacje liczb 0..n-1 w porządku leksykograficznym.</summary>
    public static IEnumerable<int[]> Permutations(int n)
    {
        var current = Enumerable.Range(0, n).ToArray();
        yield return (int[])current.Clone();

        while (true)
        {
            int i = n - 2;
            while (i >= 0 && current[i] >= current[i + 1]) i--;
            if (i < 0) yield break;

            int j = n - 1;
            while (current[j] <= current[i]) j--;
            (current[i], current[j]) = (current[j], current[i]);
            Array.Reverse(current, i + 1, n - i - 1);
            yield return (int[])current.Clone();
        }
    }
}
