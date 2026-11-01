namespace CampusAssistant.Domain;

/// <summary>Położenie miejsca na kampusie. Obiekt wartości: dwie lokalizacje o tym samym opisie są równe.</summary>
public sealed record Location
{
    public Location(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description;
    }

    public string Description { get; }
}
