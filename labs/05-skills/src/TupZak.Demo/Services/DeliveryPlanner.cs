namespace TupZak.Demo.Services;

/// <summary>Jedna dostawa: budynek, opcjonalnie konkretne wejście i potrzeba użycia wózka.</summary>
public sealed record Delivery(string BuildingId, string? EntranceId = null, bool RequiresCart = false);

/// <summary>Plan do wyznaczenia: punkt startowy, dostawy, powrót i ścieżki do ominięcia.</summary>
public sealed record DeliveryPlanRequest(
    string Start,
    IReadOnlyList<Delivery> Deliveries,
    bool ReturnToStart = false,
    IReadOnlyList<string>? AvoidPathIds = null);

public sealed record PlanPoint(string Id, string Kind, string Name, string? BuildingId, double[] Coordinates);

public sealed record DeliveryStop(
    int Sequence,
    string BuildingId,
    string BuildingName,
    string EntranceId,
    string EntranceName,
    double[] Coordinates,
    bool RequiresCart);

public sealed record RouteLeg(int Sequence, string From, string To, double DistanceM, IReadOnlyList<string> PathIds, IReadOnlyList<double[]> Coordinates);

public sealed record DeliveryPlan(
    bool Success,
    string? Error,
    PlanPoint? Start,
    IReadOnlyList<DeliveryStop>? Stops,
    IReadOnlyList<RouteLeg>? Legs,
    double? TotalDistanceM,
    bool ReturnToStart,
    bool AccessibleRouteOnly,
    IReadOnlyList<string>? AvoidedPathIds,
    string? Algorithm,
    int? EvaluatedOrders,
    IReadOnlyList<string>? Unreachable)
{
    public static DeliveryPlan Failure(string error, IReadOnlyList<string>? unreachable = null) =>
        new(false, error, null, null, null, null, false, false, null, null, null, unreachable);
}

public sealed record PointToPointRoute(
    bool Found,
    string? Error,
    PlanPoint? From,
    PlanPoint? To,
    double? DistanceM,
    IReadOnlyList<string>? PathIds,
    IReadOnlyList<double[]>? Coordinates);

public sealed record DistanceEntry(string From, string To, double? DistanceM);

public sealed record DistanceMatrix(bool Success, string? Error, IReadOnlyList<PlanPoint>? Points, IReadOnlyList<DistanceEntry>? Distances);

/// <summary>
/// Planowanie dostaw na grafie kampusu. Rozdziela dwa kroki:
///   1. koszty przejścia między punktami — najkrótsze trasy w <see cref="CampusNetwork"/>,
///   2. kolejność odwiedzania — <see cref="DeliveryOrderOptimizer"/> działający na samej macierzy kosztów.
/// Nie zależy od LLM, MCP ani mapy.
/// </summary>
public sealed class DeliveryPlanner(CampusNetwork network, BuildingRepository buildings)
{
    public const string AlgorithmDescription =
        "Dijkstra na grafie ścieżek (koszty między punktami) + przegląd wszystkich permutacji dostaw " +
        "z wyborem wejść programowaniem dynamicznym (wynik dokładny dla ≤ 8 dostaw).";

    /// <summary>Wyznacza optymalną kolejność dostaw i wejścia do budynków.</summary>
    public DeliveryPlan Optimize(DeliveryPlanRequest request)
    {
        if (request.Deliveries.Count == 0)
            return DeliveryPlan.Failure("Plan musi zawierać co najmniej jedną dostawę.");
        if (request.Deliveries.Count > DeliveryOrderOptimizer.MaxDeliveries)
            return DeliveryPlan.Failure($"Przegląd permutacji obsługuje najwyżej {DeliveryOrderOptimizer.MaxDeliveries} dostaw, a podano {request.Deliveries.Count}.");

        var (start, startError) = ResolvePoint(request.Start);
        if (start is null) return DeliveryPlan.Failure(startError!);

        var (options, optionsError) = Options(request.Deliveries.Any(d => d.RequiresCart), request.AvoidPathIds);
        if (options is null) return DeliveryPlan.Failure(optionsError!);

        // Lokalizacje w macierzy: 0 = start, dalej wejścia-kandydaci każdej dostawy.
        var locations = new List<RoutePoint> { start };
        var candidates = new List<IReadOnlyList<int>>();
        foreach (var delivery in request.Deliveries)
        {
            var (entrances, error) = Candidates(delivery);
            if (entrances is null) return DeliveryPlan.Failure(error!);

            candidates.Add(entrances.Select(e => { locations.Add(e); return locations.Count - 1; }).ToList());
        }

        var costs = Costs(locations, options);

        // Dostawa bez osiągalnego wejścia: graf jest nieskierowany, więc wystarczy sprawdzić osiągalność ze startu.
        var unreachable = request.Deliveries
            .Where((d, i) => candidates[i].All(c => double.IsPositiveInfinity(costs[0, c])))
            .Select(d => d.EntranceId ?? d.BuildingId)
            .ToList();
        if (unreachable.Count > 0)
            return DeliveryPlan.Failure(
                $"Brak trasy z {start.Id} do: {string.Join(", ", unreachable)}" +
                (options.RequireAccessible ? " (trasa tylko po ścieżkach dostępnych dla wózka)." : "."),
                unreachable);

        var solution = DeliveryOrderOptimizer.Solve(costs, 0, candidates, request.ReturnToStart)
            ?? throw new InvalidOperationException("Wszystkie dostawy są osiągalne, a optymalizator nie znalazł kolejności.");

        var stops = solution.Order
            .Select((deliveryIndex, k) => (Delivery: request.Deliveries[deliveryIndex], Entrance: locations[solution.Chosen[k]]))
            .ToList();
        return BuildPlan(start, stops, request.ReturnToStart, options, AlgorithmDescription, solution.EvaluatedOrders);
    }

    /// <summary>
    /// Trasa dla kolejności podanej z zewnątrz (np. wyznaczonej przez Skill albo kod agenta) — bez optymalizacji.
    /// Pozwala porównać i pokazać na mapie wynik każdego z wariantów.
    /// </summary>
    public DeliveryPlan Evaluate(string startId, IReadOnlyList<string> entranceIds, bool returnToStart, bool requiresCart, IReadOnlyList<string>? avoidPathIds)
    {
        if (entranceIds.Count == 0)
            return DeliveryPlan.Failure("Podaj co najmniej jedno wejście.");

        var (start, startError) = ResolvePoint(startId);
        if (start is null) return DeliveryPlan.Failure(startError!);

        var (options, optionsError) = Options(requiresCart, avoidPathIds);
        if (options is null) return DeliveryPlan.Failure(optionsError!);

        var stops = new List<(Delivery, RoutePoint)>();
        foreach (string id in entranceIds)
        {
            var entrance = network.GetPoint(id);
            if (entrance is not { Kind: "entrance" })
                return DeliveryPlan.Failure($"\"{id}\" nie jest identyfikatorem wejścia. Kolejność podaj jako identyfikatory wejść, np. A2, B1.");
            stops.Add((new Delivery(entrance.BuildingId!, entrance.Id, requiresCart), entrance));
        }

        return BuildPlan(start, stops, returnToStart, options, "kolejność podana przez wywołującego (bez optymalizacji)", null);
    }

    /// <summary>Najkrótsza trasa między dwoma punktami (budynek = jego najbliższe wejście).</summary>
    public PointToPointRoute FindRoute(string fromId, string toId, bool requiresCart, IReadOnlyList<string>? avoidPathIds)
    {
        var (from, fromError) = ResolvePoint(fromId);
        if (from is null) return new PointToPointRoute(false, fromError, null, null, null, null, null);
        var (to, toError) = ResolvePoint(toId);
        if (to is null) return new PointToPointRoute(false, toError, null, null, null, null, null);
        var (options, optionsError) = Options(requiresCart, avoidPathIds);
        if (options is null) return new PointToPointRoute(false, optionsError, null, null, null, null, null);

        var route = network.ShortestRoute(from.Nodes, to.Nodes, options);
        if (route is null)
            return new PointToPointRoute(false, $"Brak trasy z {from.Id} do {to.Id}.", null, null, null, null, null);

        return new PointToPointRoute(true, null,
            ToPlanPoint(from, route.Nodes[0]), ToPlanPoint(to, route.Nodes[^1]),
            route.Length, route.PathIds, Coordinates(route));
    }

    /// <summary>
    /// Odległości po ścieżkach między każdą parą punktów. Budynek jest rozwijany do wszystkich jego wejść,
    /// bo koszt zależy od wybranego wejścia. Wynik ma postać par (from, to, distanceM) — gotową do użycia w skrypcie.
    /// </summary>
    public DistanceMatrix Matrix(IReadOnlyList<string> pointIds, bool requiresCart, IReadOnlyList<string>? avoidPathIds)
    {
        var (options, optionsError) = Options(requiresCart, avoidPathIds);
        if (options is null) return new DistanceMatrix(false, optionsError, null, null);

        var points = new List<RoutePoint>();
        foreach (string id in pointIds)
        {
            var building = buildings.GetById(id);
            if (building is not null)
            {
                points.AddRange(building.Entrances.Select(e => network.GetPoint(e.Id)!));
                continue;
            }

            var (point, error) = ResolvePoint(id);
            if (point is null) return new DistanceMatrix(false, error, null, null);
            points.Add(point);
        }
        points = points.DistinctBy(p => p.Id).ToList();

        var costs = Costs(points, options);
        var distances = new List<DistanceEntry>();
        for (int i = 0; i < points.Count; i++)
            for (int j = i + 1; j < points.Count; j++)
                distances.Add(new DistanceEntry(points[i].Id, points[j].Id,
                    double.IsPositiveInfinity(costs[i, j]) ? null : Math.Round(costs[i, j], 1)));

        return new DistanceMatrix(true, null, points.Select(p => ToPlanPoint(p, p.Nodes.FirstOrDefault(-1))).ToList(), distances);
    }

    /// <summary>Punkt trasy: wejście, parking, punkt orientacyjny albo budynek (= wszystkie jego wejścia).</summary>
    public (RoutePoint? Point, string? Error) ResolvePoint(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return (null, "Identyfikator punktu nie może być pusty.");

        var point = network.GetPoint(id);
        var building = point is null ? buildings.Find(id) : null;
        if (building is not null)
            point = new RoutePoint(building.Id, "building", building.Name, building.Id,
                building.Entrances.SelectMany(e => network.GetPoint(e.Id)!.Nodes).ToList());

        if (point is null)
        {
            // „P1” pasuje do A-P1, B-P1 i C-P1 — zamiast zgadywać, zwracamy listę kandydatów.
            var similar = network.Points
                .Where(p => p.Id.EndsWith("-" + id.Trim(), StringComparison.OrdinalIgnoreCase)
                            || (id.Trim().Length >= 3 && p.Name.Contains(id.Trim(), StringComparison.OrdinalIgnoreCase)))
                .Select(p => $"{p.Id} ({p.Name})")
                .ToList();
            return (null, similar.Count > 0
                ? $"Punkt \"{id}\" jest niejednoznaczny lub nie istnieje. Pasujące punkty: {string.Join(", ", similar)}."
                : $"Nie znaleziono punktu \"{id}\". Użyj identyfikatora wejścia (np. A1), parkingu (np. A-P1), punktu orientacyjnego (np. A-L1) albo budynku (np. A).");
        }

        if (point.Nodes.Count == 0)
            return (null, $"Punkt {point.Id} nie jest połączony z siecią ścieżek.");

        return (point, null);
    }

    private (IReadOnlyList<RoutePoint>? Entrances, string? Error) Candidates(Delivery delivery)
    {
        var building = buildings.GetById(delivery.BuildingId);
        if (building is null)
            return (null, $"Budynek \"{delivery.BuildingId}\" nie istnieje. Dostępne budynki: {string.Join(", ", buildings.All.Select(b => b.Id))}.");

        if (string.IsNullOrWhiteSpace(delivery.EntranceId))
            return (building.Entrances.Select(e => network.GetPoint(e.Id)!).Where(p => p.Nodes.Count > 0).ToList(), null);

        var entrance = building.Entrances.FirstOrDefault(e => e.Id.Equals(delivery.EntranceId.Trim(), StringComparison.OrdinalIgnoreCase));
        if (entrance is null)
            return (null, $"Wejście \"{delivery.EntranceId}\" nie należy do budynku {building.Id}. Wejścia: {string.Join(", ", building.Entrances.Select(e => e.Id))}.");

        return ([network.GetPoint(entrance.Id)!], null);
    }

    private (PathConstraints? Options, string? Error) Options(bool requiresCart, IReadOnlyList<string>? avoidPathIds)
    {
        var avoid = (avoidPathIds ?? []).Select(p => p.Trim()).Where(p => p.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = avoid.Where(p => !network.Paths.ContainsKey(p)).ToList();
        if (unknown.Count > 0)
            return (null, $"Nieznane ścieżki do ominięcia: {string.Join(", ", unknown)}.");

        return (new PathConstraints(requiresCart, avoid), null);
    }

    /// <summary>Macierz kosztów: Dijkstra z każdej lokalizacji (lokalizacja może mieć kilka węzłów, np. parking).</summary>
    private double[,] Costs(IReadOnlyList<RoutePoint> locations, PathConstraints options)
    {
        var costs = new double[locations.Count, locations.Count];
        for (int i = 0; i < locations.Count; i++)
        {
            var (distance, _) = network.ShortestPaths(locations[i].Nodes, options);
            for (int j = 0; j < locations.Count; j++)
                costs[i, j] = i == j ? 0 : locations[j].Nodes.Select(n => distance[n]).DefaultIfEmpty(double.PositiveInfinity).Min();
        }
        return costs;
    }

    private DeliveryPlan BuildPlan(
        RoutePoint start, IReadOnlyList<(Delivery Delivery, RoutePoint Entrance)> stops, bool returnToStart,
        PathConstraints options, string algorithm, int? evaluatedOrders)
    {
        var legs = new List<RouteLeg>();
        IReadOnlyCollection<int> current = start.Nodes;
        string currentId = start.Id;
        int? startNode = null;

        var targets = stops.Select(s => (Id: s.Entrance.Id, Nodes: (IReadOnlyCollection<int>)s.Entrance.Nodes)).ToList();
        if (returnToStart) targets.Add((start.Id, start.Nodes));

        foreach (var (id, nodes) in targets)
        {
            var route = network.ShortestRoute(current, nodes, options);
            if (route is null)
                return DeliveryPlan.Failure($"Brak trasy z {currentId} do {id}.", [id]);

            startNode ??= route.Nodes[0];
            legs.Add(new RouteLeg(legs.Count + 1, currentId, id, route.Length, route.PathIds, Coordinates(route)));
            current = [route.Nodes[^1]];
            currentId = id;
        }

        var planStops = stops.Select((s, k) =>
        {
            var building = buildings.GetById(s.Entrance.BuildingId)!;
            var entrance = building.Entrances.Single(e => e.Id == s.Entrance.Id);
            return new DeliveryStop(k + 1, building.Id, building.Name, entrance.Id, entrance.Name, entrance.Points[0].ToArray(), s.Delivery.RequiresCart);
        }).ToList();

        return new DeliveryPlan(
            true, null, ToPlanPoint(start, startNode ?? start.Nodes[0]), planStops, legs,
            Math.Round(legs.Sum(l => l.DistanceM), 1), returnToStart, options.RequireAccessible,
            options.AvoidPathIds?.Order().ToList(), algorithm, evaluatedOrders, null);
    }

    private PlanPoint ToPlanPoint(RoutePoint point, int node) =>
        new(point.Id, point.Kind, point.Name, point.BuildingId, node >= 0 ? network.Nodes[node].ToArray() : []);

    private IReadOnlyList<double[]> Coordinates(NetworkRoute route) =>
        route.Nodes.Select(n => network.Nodes[n].ToArray()).ToList();
}
