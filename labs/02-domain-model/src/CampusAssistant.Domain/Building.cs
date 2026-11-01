namespace CampusAssistant.Domain;

/// <summary>Obiekt na kampusie. Mogą się w nim znajdować sale, które można utworzyć tylko przez tę klasę.</summary>
public sealed class Building : Place
{
    private readonly List<Room> _rooms = [];

    internal Building(string name, Location location)
        : base(name, location)
    {
    }

    public IReadOnlyList<Room> Rooms => _rooms.AsReadOnly();

    public Room AddRoom(string number, Location location)
    {
        var room = new Room(number, location, this);
        _rooms.Add(room);
        return room;
    }
}
