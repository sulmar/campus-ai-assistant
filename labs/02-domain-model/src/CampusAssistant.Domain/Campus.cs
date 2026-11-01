namespace CampusAssistant.Domain;

/// <summary>Teren uczelni. Jest właścicielem budynków i automatów z kawą, więc można je utworzyć tylko przez tę klasę.</summary>
public sealed class Campus
{
    private readonly List<Building> _buildings = [];
    private readonly List<CoffeeMachine> _coffeeMachines = [];

    public IReadOnlyList<Building> Buildings => _buildings.AsReadOnly();

    public IReadOnlyList<CoffeeMachine> CoffeeMachines => _coffeeMachines.AsReadOnly();

    public Building AddBuilding(string name, Location location)
    {
        var building = new Building(name, location);
        _buildings.Add(building);
        return building;
    }

    /// <param name="building">Budynek, w którym stoi automat, lub <see langword="null"/>, jeśli stoi poza budynkiem.</param>
    public CoffeeMachine AddCoffeeMachine(
        string name,
        Location location,
        Availability availability = Availability.Unknown,
        Building? building = null)
    {
        if (building is not null && !_buildings.Contains(building))
            throw new ArgumentException("The building does not belong to this campus.", nameof(building));

        var coffeeMachine = new CoffeeMachine(name, location, availability, building);
        _coffeeMachines.Add(coffeeMachine);
        return coffeeMachine;
    }
}
