namespace CampusAssistant.Domain;

/// <summary>Pomieszczenie w budynku. Jego nazwą jest numer, np. A101.</summary>
public sealed class Room : Place
{
    internal Room(string number, Location location, Building building)
        : base(number, location)
    {
        Building = building;
    }

    public Building Building { get; }
}
