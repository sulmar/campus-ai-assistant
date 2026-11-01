namespace CampusAssistant.Domain;

/// <summary>Sala, budynek lub automat z kawą, czyli to, co student może wyszukać i co ma lokalizację.</summary>
public abstract class Place
{
    protected Place(string name, Location location)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(location);

        Name = name;
        Location = location;
    }

    public string Name { get; }

    public Location Location { get; }
}
