namespace TupZak.Demo.Services;

/// <summary>Punkt w lokalnym układzie współrzędnych (metry): X rośnie na wschód, Y na północ.</summary>
public readonly record struct Point2(double X, double Y)
{
    public double[] ToArray() => [X, Y];
}

/// <summary>
/// Proste obliczenia geometryczne potrzebne narzędziom.
/// Aplikacja liczy fakty (kierunki, odległości), a model na ich podstawie rozumuje.
/// </summary>
public static class Geometry
{
    public static double SignedArea(IReadOnlyList<Point2> ring)
    {
        double sum = 0;
        for (int i = 0; i < ring.Count - 1; i++)
            sum += ring[i].X * ring[i + 1].Y - ring[i + 1].X * ring[i].Y;
        return sum / 2;
    }

    /// <summary>Środek ciężkości wielokąta (pierwszy i ostatni punkt pierścienia są takie same).</summary>
    public static Point2 Centroid(IReadOnlyList<Point2> ring)
    {
        double area = SignedArea(ring);
        double cx = 0, cy = 0;
        for (int i = 0; i < ring.Count - 1; i++)
        {
            double cross = ring[i].X * ring[i + 1].Y - ring[i + 1].X * ring[i].Y;
            cx += (ring[i].X + ring[i + 1].X) * cross;
            cy += (ring[i].Y + ring[i + 1].Y) * cross;
        }
        return new Point2(Math.Round(cx / (6 * area), 1), Math.Round(cy / (6 * area), 1));
    }

    public static Point2 Average(IReadOnlyList<Point2> points) =>
        new(Math.Round(points.Average(p => p.X), 1), Math.Round(points.Average(p => p.Y), 1));

    public static double Distance(Point2 a, Point2 b) =>
        Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    public static double DistanceToSegment(Point2 p, Point2 a, Point2 b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y;
        double lengthSquared = dx * dx + dy * dy;
        if (lengthSquared == 0) return Distance(p, a);
        double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / lengthSquared, 0, 1);
        return Distance(p, new Point2(a.X + t * dx, a.Y + t * dy));
    }

    /// <summary>Odległość od punktu do łamanej (lub pierścienia).</summary>
    public static double DistanceToPolyline(Point2 p, IReadOnlyList<Point2> line)
    {
        if (line.Count == 1) return Distance(p, line[0]);
        double min = double.MaxValue;
        for (int i = 0; i < line.Count - 1; i++)
            min = Math.Min(min, DistanceToSegment(p, line[i], line[i + 1]));
        return min;
    }

    public static bool IsInside(Point2 p, IReadOnlyList<Point2> ring)
    {
        bool inside = false;
        for (int i = 0, j = ring.Count - 1; i < ring.Count; j = i++)
        {
            if ((ring[i].Y > p.Y) != (ring[j].Y > p.Y) &&
                p.X < (ring[j].X - ring[i].X) * (p.Y - ring[i].Y) / (ring[j].Y - ring[i].Y) + ring[i].X)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Odległość od punktu do wielokąta: 0, jeżeli punkt leży wewnątrz.</summary>
    public static double DistanceToPolygon(Point2 p, IReadOnlyList<Point2> ring) =>
        IsInside(p, ring) ? 0 : DistanceToPolyline(p, ring);

    /// <summary>
    /// Kierunek, w którym „patrzy” ściana budynku najbliższa punktowi
    /// (wektor normalny skierowany na zewnątrz obrysu).
    /// </summary>
    public static string FacadeDirection(Point2 p, IReadOnlyList<Point2> ring)
    {
        int nearest = 0;
        double min = double.MaxValue;
        for (int i = 0; i < ring.Count - 1; i++)
        {
            double d = DistanceToSegment(p, ring[i], ring[i + 1]);
            if (d < min) { min = d; nearest = i; }
        }

        var a = ring[nearest];
        var b = ring[nearest + 1];
        double dx = b.X - a.X, dy = b.Y - a.Y;

        // Dla pierścienia przeciwnego do ruchu wskazówek zegara normalna zewnętrzna to (dy, -dx).
        bool counterClockwise = SignedArea(ring) > 0;
        double nx = counterClockwise ? dy : -dy;
        double ny = counterClockwise ? -dx : dx;
        return CompassDirection(nx, ny);
    }

    /// <summary>Kierunek od punktu <paramref name="from"/> do <paramref name="to"/>.</summary>
    public static string Direction(Point2 from, Point2 to) => CompassDirection(to.X - from.X, to.Y - from.Y);

    /// <summary>Zamienia wektor na jeden z 8 kierunków geograficznych.</summary>
    public static string CompassDirection(double dx, double dy)
    {
        if (dx == 0 && dy == 0) return "brak (ten sam punkt)";
        string[] names =
        [
            "wschód (E)", "północny wschód (NE)", "północ (N)", "północny zachód (NW)",
            "zachód (W)", "południowy zachód (SW)", "południe (S)", "południowy wschód (SE)"
        ];
        double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        int index = (int)Math.Round(((angle % 360) + 360) % 360 / 45) % 8;
        return names[index];
    }
}
