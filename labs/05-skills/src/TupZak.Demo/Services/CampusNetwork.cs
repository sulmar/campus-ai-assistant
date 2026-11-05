using System.Text.Json.Nodes;

namespace TupZak.Demo.Services;

/// <summary>Ścieżka piesza: odcinek przy budynku albo aleja kampusu.</summary>
public sealed record CampusPath(string Id, string Name, bool Accessible, bool Closed, IReadOnlyList<Point2> Points);

/// <summary>Krawędź grafu: fragment ścieżki między dwoma węzłami.</summary>
public sealed record NetworkEdge(int From, int To, double Length, string PathId);

/// <summary>
/// Punkt, z którego lub do którego można wyznaczyć trasę: wejście, parking albo punkt orientacyjny.
/// Parking może mieć kilka węzłów dostępu (miejsc, w których ścieżki dochodzą do jego krawędzi).
/// </summary>
public sealed record RoutePoint(string Id, string Kind, string Name, string? BuildingId, IReadOnlyList<int> Nodes);

/// <summary>Ograniczenia trasy: tylko ścieżki dostępne dla wózka i lista ścieżek do ominięcia.</summary>
public sealed record PathConstraints(bool RequireAccessible = false, IReadOnlySet<string>? AvoidPathIds = null)
{
    public static PathConstraints Default { get; } = new();
}

/// <summary>Najkrótsza trasa w grafie ścieżek.</summary>
public sealed record NetworkRoute(double Length, IReadOnlyList<int> Nodes, IReadOnlyList<string> PathIds);

/// <summary>
/// Graf ścieżek kampusu zbudowany ze ścieżek przy budynkach (pliki building-*.geojson)
/// i alej kampusu (campus-paths.geojson). Węzły to wierzchołki ścieżek i ich skrzyżowania,
/// krawędzie to odcinki z długością w metrach. Wyznacza najkrótsze trasy algorytmem Dijkstry.
/// </summary>
public sealed class CampusNetwork
{
    /// <summary>Wierzchołki bliższe niż ta odległość są jednym węzłem (połączenie ścieżek).</summary>
    public const double JoinTolerance = 0.01;

    /// <summary>Wejście i parking łączą się z siecią, jeżeli ścieżka dochodzi do nich na tę odległość.</summary>
    public const double AccessTolerance = 0.5;

    /// <summary>Punkt orientacyjny (np. przystanek) jest przyciągany do najbliższej ścieżki w tej odległości.</summary>
    public const double LandmarkSnapDistance = 5;

    private readonly List<Point2> _nodes = [];
    private readonly Dictionary<(long, long), int> _nodeIndex = [];
    private readonly List<NetworkEdge> _edges = [];
    private readonly List<List<int>> _adjacency = [];
    private readonly Dictionary<string, CampusPath> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RoutePoint> _points = new(StringComparer.OrdinalIgnoreCase);

    private CampusNetwork() { }

    public IReadOnlyList<Point2> Nodes => _nodes;
    public IReadOnlyList<NetworkEdge> Edges => _edges;
    public IReadOnlyDictionary<string, CampusPath> Paths => _paths;
    public IEnumerable<RoutePoint> Points => _points.Values;

    public RoutePoint? GetPoint(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : _points.GetValueOrDefault(id.Trim());

    public static CampusNetwork LoadFromDirectory(string directory, BuildingRepository buildings)
    {
        string file = Path.Combine(directory, "campus-paths.geojson");
        var campusPaths = File.Exists(file)
            ? ReadPaths(JsonNode.Parse(File.ReadAllText(file))!, Path.GetFileName(file))
            : [];
        return Build(buildings, campusPaths);
    }

    /// <summary>Odczytuje ścieżki (kind=path, LineString) z FeatureCollection wraz z atrybutami accessible i closed.</summary>
    public static IReadOnlyList<CampusPath> ReadPaths(JsonNode featureCollection, string sourceName)
    {
        var paths = new List<CampusPath>();
        foreach (var feature in featureCollection["features"]?.AsArray() ?? [])
        {
            var properties = feature?["properties"];
            if ((string?)properties?["kind"] != "path") continue;

            string id = (string?)properties!["id"] ?? throw new InvalidDataException($"{sourceName}: ścieżka bez properties.id.");
            var geometry = feature!["geometry"];
            if ((string?)geometry?["type"] != "LineString")
                throw new InvalidDataException($"{sourceName}: ścieżka {id} powinna mieć geometrię LineString.");

            var points = geometry!["coordinates"]!.AsArray()
                .Select(c => new Point2(c![0]!.GetValue<double>(), c[1]!.GetValue<double>()))
                .ToList();
            if (points.Count < 2)
                throw new InvalidDataException($"{sourceName}: ścieżka {id} musi mieć co najmniej dwa punkty.");

            paths.Add(new CampusPath(
                id,
                (string?)properties["name"] ?? id,
                (bool?)properties["accessible"] ?? true,
                (bool?)properties["closed"] ?? false,
                points));
        }
        return paths;
    }

    public static CampusNetwork Build(BuildingRepository buildings, IEnumerable<CampusPath> campusPaths)
    {
        var network = new CampusNetwork();

        var allPaths = buildings.All.SelectMany(b => ReadPaths(b.GeoJson, b.Id)).Concat(campusPaths).ToList();
        foreach (var path in allPaths)
        {
            if (!network._paths.TryAdd(path.Id, path))
                throw new InvalidDataException($"Ścieżka o identyfikatorze '{path.Id}' występuje wielokrotnie.");
        }

        // 1. Punkty podziału: wszystkie wierzchołki ścieżek oraz rzuty punktów orientacyjnych na najbliższą ścieżkę.
        var splitPoints = allPaths.SelectMany(p => p.Points).ToList();
        var landmarkProjections = new Dictionary<MapFeature, Point2>();
        foreach (var landmark in buildings.All.SelectMany(b => b.Features).Where(f => f.Kind is "landmark"))
        {
            var (projection, distance) = NearestPointOnPaths(landmark.Points[0], allPaths);
            if (distance <= LandmarkSnapDistance)
            {
                landmarkProjections[landmark] = projection;
                splitPoints.Add(projection);
            }
        }

        // 2. Krawędzie: każdy odcinek ścieżki dzielimy w punktach, które na nim leżą (skrzyżowania typu T).
        foreach (var path in allPaths)
        {
            for (int i = 0; i < path.Points.Count - 1; i++)
            {
                var a = path.Points[i];
                var b = path.Points[i + 1];
                var onSegment = splitPoints
                    .Where(p => Geometry.DistanceToSegment(p, a, b) < JoinTolerance)
                    .Select(p => (Point: p, T: Geometry.Distance(a, p)))
                    .OrderBy(x => x.T)
                    .Select(x => network.NodeAt(x.Point))
                    .Distinct()
                    .ToList();

                for (int j = 0; j < onSegment.Count - 1; j++)
                    network.AddEdge(onSegment[j], onSegment[j + 1], path.Id);
            }
        }

        // 3. Punkty tras: wejścia, parkingi i punkty orientacyjne połączone z siecią.
        foreach (var building in buildings.All)
        {
            foreach (var entrance in building.Entrances)
                network.AddPoint(entrance.Id, "entrance", $"{entrance.Name} ({building.Name})", building.Id,
                    network.NodesNear(n => Geometry.Distance(n, entrance.Points[0]) <= AccessTolerance));

            foreach (var feature in building.Features)
            {
                if (feature.Kind == "parking")
                {
                    var access = network.NodesNear(n => Geometry.DistanceToPolyline(n, feature.Points) <= AccessTolerance);
                    network.AddPoint(feature.Id, "parking", $"{feature.Name} przy budynku {building.Id}", building.Id, access);

                    // Parking można przejść na skos: łączymy jego węzły dostępu krawędziami w linii prostej.
                    if (access.Count > 1)
                    {
                        network._paths[feature.Id] = new CampusPath(feature.Id, $"Przejście przez parking {feature.Id}", true, false, feature.Points);
                        for (int i = 0; i < access.Count; i++)
                            for (int j = i + 1; j < access.Count; j++)
                                network.AddEdge(access[i], access[j], feature.Id);
                    }
                }
                else if (feature.Kind == "landmark")
                    network.AddPoint(feature.Id, "landmark", $"{feature.Name} przy budynku {building.Id}", building.Id,
                        landmarkProjections.TryGetValue(feature, out var projection) ? [network.NodeAt(projection)] : []);
            }
        }

        return network;
    }

    /// <summary>Czy po krawędzi wolno przejść przy podanych ograniczeniach?</summary>
    public bool IsUsable(NetworkEdge edge, PathConstraints options)
    {
        var path = _paths[edge.PathId];
        return !path.Closed
               && !(options.AvoidPathIds?.Contains(edge.PathId) ?? false)
               && (!options.RequireAccessible || path.Accessible);
    }

    /// <summary>Odległości od zbioru węzłów źródłowych do wszystkich węzłów (Dijkstra, wiele źródeł).</summary>
    public (double[] Distance, int[] PreviousEdge) ShortestPaths(IEnumerable<int> sources, PathConstraints options)
    {
        var distance = Enumerable.Repeat(double.PositiveInfinity, _nodes.Count).ToArray();
        var previousEdge = Enumerable.Repeat(-1, _nodes.Count).ToArray();
        var queue = new PriorityQueue<int, double>();

        foreach (int source in sources)
        {
            distance[source] = 0;
            queue.Enqueue(source, 0);
        }

        while (queue.TryDequeue(out int node, out double d))
        {
            if (d > distance[node]) continue;
            foreach (int edgeIndex in _adjacency[node])
            {
                var edge = _edges[edgeIndex];
                if (!IsUsable(edge, options)) continue;

                int next = edge.From == node ? edge.To : edge.From;
                double candidate = d + edge.Length;
                if (candidate < distance[next])
                {
                    distance[next] = candidate;
                    previousEdge[next] = edgeIndex;
                    queue.Enqueue(next, candidate);
                }
            }
        }

        return (distance, previousEdge);
    }

    /// <summary>Najkrótsza trasa z dowolnego węzła źródłowego do dowolnego węzła docelowego albo null, gdy trasy nie ma.</summary>
    public NetworkRoute? ShortestRoute(IReadOnlyCollection<int> sources, IReadOnlyCollection<int> targets, PathConstraints options)
    {
        if (sources.Count == 0 || targets.Count == 0) return null;

        var (distance, previousEdge) = ShortestPaths(sources, options);
        int target = targets.MinBy(t => distance[t]);
        if (double.IsPositiveInfinity(distance[target])) return null;

        var nodes = new List<int> { target };
        var pathIds = new List<string>();
        for (int node = target; previousEdge[node] >= 0;)
        {
            var edge = _edges[previousEdge[node]];
            node = edge.From == node ? edge.To : edge.From;
            nodes.Add(node);
            if (pathIds.Count == 0 || pathIds[^1] != edge.PathId)
                pathIds.Add(edge.PathId);
        }
        nodes.Reverse();
        pathIds.Reverse();

        return new NetworkRoute(Math.Round(distance[target], 1), nodes, pathIds);
    }

    private int NodeAt(Point2 point)
    {
        var key = ((long)Math.Round(point.X / JoinTolerance), (long)Math.Round(point.Y / JoinTolerance));
        if (_nodeIndex.TryGetValue(key, out int index)) return index;

        _nodes.Add(point);
        _adjacency.Add([]);
        return _nodeIndex[key] = _nodes.Count - 1;
    }

    private void AddEdge(int from, int to, string pathId)
    {
        if (from == to) return;
        _edges.Add(new NetworkEdge(from, to, Geometry.Distance(_nodes[from], _nodes[to]), pathId));
        _adjacency[from].Add(_edges.Count - 1);
        _adjacency[to].Add(_edges.Count - 1);
    }

    private List<int> NodesNear(Func<Point2, bool> predicate) =>
        Enumerable.Range(0, _nodes.Count).Where(i => _adjacency[i].Count > 0 && predicate(_nodes[i])).ToList();

    private void AddPoint(string id, string kind, string name, string? buildingId, IReadOnlyList<int> nodes)
    {
        if (!_points.TryAdd(id, new RoutePoint(id, kind, name, buildingId, nodes)))
            throw new InvalidDataException($"Punkt o identyfikatorze '{id}' występuje wielokrotnie.");
    }

    private static (Point2 Point, double Distance) NearestPointOnPaths(Point2 p, IEnumerable<CampusPath> paths)
    {
        var best = (Point: p, Distance: double.PositiveInfinity);
        foreach (var path in paths)
        {
            for (int i = 0; i < path.Points.Count - 1; i++)
            {
                var a = path.Points[i];
                var b = path.Points[i + 1];
                double dx = b.X - a.X, dy = b.Y - a.Y;
                double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
                var projection = new Point2(Math.Round(a.X + t * dx, 2), Math.Round(a.Y + t * dy, 2));
                double distance = Geometry.Distance(p, projection);
                if (distance < best.Distance) best = (projection, distance);
            }
        }
        return best;
    }
}
