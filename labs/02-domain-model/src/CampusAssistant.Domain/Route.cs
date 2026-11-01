namespace CampusAssistant.Domain;

/// <summary>Droga łącząca dwie różne lokalizacje.</summary>
public sealed class Route
{
    public Route(Location start, Location destination, IEnumerable<string> directions)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(directions);

        if (start == destination)
            throw new ArgumentException("A route must connect two different locations.", nameof(destination));

        Start = start;
        Destination = destination;
        Directions = directions.ToList().AsReadOnly();
    }

    public Location Start { get; }

    public Location Destination { get; }

    public IReadOnlyList<string> Directions { get; }
}
