using System.ComponentModel;
using Microsoft.Extensions.AI;
using TupZak.Demo.Services;

namespace TupZak.Demo.Tools;

/// <summary>
/// Narzędzia planowania dostaw. Cienka warstwa nad <see cref="DeliveryPlanner"/> i <see cref="CampusNetwork"/>:
/// obliczenia są w domenie, tutaj tylko opisy dla modelu, mapowanie argumentów i aktualizacja mapy.
///
/// Narzędzia celowo udostępniają trzy poziomy, żeby porównać warianty rozwiązania tego samego problemu:
///   - optimize_deliveries — gotowy algorytm w aplikacji,
///   - get_distance_matrix — same koszty; kolejność może policzyć skrypt Skilla,
///   - get_campus_graph — surowy graf; trasy i kolejność może policzyć kod wygenerowany przez agenta.
/// Wynik każdego wariantu można pokazać na mapie narzędziem show_delivery_plan.
/// </summary>
public sealed class DeliveryTools(CampusNetwork network, DeliveryPlanner planner, MapState mapState)
{
    public const string FindPlaceName = "find_place";
    public const string GetCampusGraphName = "get_campus_graph";
    public const string FindRouteName = "find_route";
    public const string GetDistanceMatrixName = "get_distance_matrix";
    public const string OptimizeDeliveriesName = "optimize_deliveries";

    public IReadOnlyList<AIFunction> CreateAIFunctions() =>
    [
        AIFunctionFactory.Create(FindPlace, FindPlaceName,
            "Wyszukuje punkty, z których i do których można wyznaczyć trasę: parkingi, wejścia, punkty orientacyjne i budynki. " +
            "Zwraca wszystkie pasujące punkty — jeżeli jest ich kilka, zapytaj użytkownika, o który chodzi.",
            CampusTools.JsonOptions),
        AIFunctionFactory.Create(GetCampusGraph, GetCampusGraphName,
            "Zwraca graf ścieżek kampusu: węzły ze współrzędnymi, krawędzie z długością w metrach i dostępnością dla wózka " +
            "oraz punkty tras (wejścia, parkingi, przystanki) z ich węzłami. Służy do samodzielnych obliczeń.",
            CampusTools.JsonOptions),
        AIFunctionFactory.Create(FindRoute, FindRouteName,
            "Wyznacza najkrótszą trasę po ścieżkach między dwoma punktami. Budynek oznacza jego najbliższe wejście.",
            CampusTools.JsonOptions),
        AIFunctionFactory.Create(GetDistanceMatrix, GetDistanceMatrixName,
            "Zwraca odległości po ścieżkach (w metrach) między każdą parą podanych punktów. Budynek jest rozwijany " +
            "do wszystkich jego wejść. Para bez distanceM oznacza brak trasy.",
            CampusTools.JsonOptions),
        AIFunctionFactory.Create(OptimizeDeliveries, OptimizeDeliveriesName,
            "Wyznacza optymalną kolejność dostaw do budynków i wybiera wejścia, minimalizując łączną długość trasy po ścieżkach. " +
            "Zwraca etapy trasy, długości i łączny dystans oraz pokazuje plan na mapie.",
            CampusTools.JsonOptions),
    ];

    public FindPlaceResult FindPlace(
        [Description("Nazwa lub identyfikator punktu, np. \"P1\", \"A-P1\", \"przystanek\", \"B2\" albo \"Budynek C\".")] string query)
    {
        string q = query.Trim();
        // „P1” pasuje do A-P1, B-P1 i C-P1; po nazwie szukamy dopiero od 3 znaków, żeby „A” nie pasowało do wszystkiego.
        var matches = network.Points
            .Where(p => p.Id.Equals(q, StringComparison.OrdinalIgnoreCase)
                        || p.Id.EndsWith("-" + q, StringComparison.OrdinalIgnoreCase)
                        || (q.Length >= 3 && p.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
            .Select(p => new PlaceInfo(p.Id, p.Kind, p.Name, p.BuildingId, p.Nodes.Count > 0))
            .ToList();

        var (building, _) = planner.ResolvePoint(q);
        if (building is { Kind: "building" })
            matches.Insert(0, new PlaceInfo(building.Id, "building", building.Name, building.Id, true));

        return matches.Count == 0
            ? new FindPlaceResult(false, [], $"Nie znaleziono punktu pasującego do \"{query}\".")
            : new FindPlaceResult(true, matches, null);
    }

    public CampusGraphResult GetCampusGraph(
        [Description("true, jeżeli kurier ma wózek: pomija ścieżki niedostępne (schody).")] bool requiresCart = false,
        [Description("Identyfikatory ścieżek do ominięcia, np. [\"W-02\"].")] string[]? avoidPathIds = null)
    {
        var options = new PathConstraints(requiresCart, (avoidPathIds ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase));
        return new CampusGraphResult(
            "Lokalny układ współrzędnych w metrach: x rośnie na wschód, y rośnie na północ. Graf jest nieskierowany.",
            network.Nodes.Select((n, i) => new GraphNode(i, n.X, n.Y)).ToList(),
            network.Edges.Where(e => network.IsUsable(e, options))
                .Select(e => new GraphEdge(e.From, e.To, Math.Round(e.Length, 2), e.PathId, network.Paths[e.PathId].Accessible))
                .ToList(),
            network.Points.Where(p => p.Nodes.Count > 0).Select(p => new GraphPoint(p.Id, p.Kind, p.Name, p.BuildingId, p.Nodes)).ToList());
    }

    public PointToPointRoute FindRoute(
        [Description("Punkt startowy: wejście (A1), parking (A-P1), punkt orientacyjny (A-L1) albo budynek (A).")] string from,
        [Description("Punkt docelowy w tym samym formacie.")] string to,
        [Description("true, jeżeli kurier ma wózek: pomija ścieżki niedostępne (schody).")] bool requiresCart = false,
        [Description("Identyfikatory ścieżek do ominięcia, np. [\"W-02\"].")] string[]? avoidPathIds = null) =>
        planner.FindRoute(from, to, requiresCart, avoidPathIds);

    public DistanceMatrix GetDistanceMatrix(
        [Description("Identyfikatory punktów, np. [\"A-P1\", \"A\", \"B\", \"C\"]. Budynek jest rozwijany do wszystkich wejść.")] string[] points,
        [Description("true, jeżeli kurier ma wózek: pomija ścieżki niedostępne (schody).")] bool requiresCart = false,
        [Description("Identyfikatory ścieżek do ominięcia, np. [\"W-02\"].")] string[]? avoidPathIds = null) =>
        planner.Matrix(points, requiresCart, avoidPathIds);

    public DeliveryPlan OptimizeDeliveries(
        [Description("Punkt startowy kuriera, np. parking \"A-P1\". Gdy użytkownik poda niejednoznaczną nazwę (np. \"P1\"), najpierw użyj find_place.")] string start,
        [Description("Dostawy: budynek, opcjonalnie konkretne wejście i potrzeba wózka.")] DeliveryInput[] deliveries,
        [Description("true, jeżeli kurier ma wrócić do punktu startowego.")] bool returnToStart = false,
        [Description("Identyfikatory ścieżek do ominięcia (np. zamkniętych), np. [\"W-02\"].")] string[]? avoidPathIds = null)
    {
        var plan = planner.Optimize(new DeliveryPlanRequest(
            start,
            deliveries.Select(d => new Delivery(d.BuildingId, string.IsNullOrWhiteSpace(d.EntranceId) ? null : d.EntranceId, d.RequiresCart)).ToList(),
            returnToStart,
            avoidPathIds));

        if (plan.Success) mapState.ShowDeliveryPlan(plan);
        return plan;
    }
}

public sealed record DeliveryInput(
    [property: Description("Identyfikator budynku, np. \"B\".")] string BuildingId,
    [property: Description("Opcjonalnie konkretne wejście, np. \"B2\". Pomiń, żeby wybrać najlepsze.")] string? EntranceId = null,
    [property: Description("true, jeżeli ta paczka wymaga wózka (wtedy cała trasa omija schody).")] bool RequiresCart = false);

public sealed record PlaceInfo(string Id, string Kind, string Name, string? BuildingId, bool Connected);

public sealed record FindPlaceResult(bool Found, IReadOnlyList<PlaceInfo> Places, string? Error);

public sealed record GraphNode(int Id, double X, double Y);

public sealed record GraphEdge(int From, int To, double LengthM, string PathId, bool Accessible);

public sealed record GraphPoint(string Id, string Kind, string Name, string? BuildingId, IReadOnlyList<int> Nodes);

public sealed record CampusGraphResult(string CoordinateSystem, IReadOnlyList<GraphNode> Nodes, IReadOnlyList<GraphEdge> Edges, IReadOnlyList<GraphPoint> Points);
