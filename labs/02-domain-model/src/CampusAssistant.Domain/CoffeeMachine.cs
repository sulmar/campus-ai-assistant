namespace CampusAssistant.Domain;

/// <summary>Urządzenie umożliwiające samodzielny zakup kawy. Może stać w budynku lub poza nim.</summary>
public sealed class CoffeeMachine : Place
{
    internal CoffeeMachine(string name, Location location, Availability availability, Building? building)
        : base(name, location)
    {
        Availability = availability;
        Building = building;
    }

    public Availability Availability { get; set; }

    public Building? Building { get; }
}
