namespace CampusAssistant.Domain.Features.SearchPlace;

/// <summary>Możliwe wyniki UC-01. Prywatny konstruktor sprawia, że poza tą klasą nie można dodać nowego wyniku.</summary>
public abstract record SearchPlaceResult
{
    private SearchPlaceResult()
    {
    }

    /// <summary>Scenariusz podstawowy: pasuje dokładnie jedno miejsce.</summary>
    public sealed record Found(string Name, string Location) : SearchPlaceResult;

    /// <summary>Scenariusz alternatywny 3a: nie pasuje żadne miejsce.</summary>
    public sealed record NotFound : SearchPlaceResult;

    /// <summary>Scenariusz alternatywny 3b: pasuje kilka miejsc, więc student musi doprecyzować wybór.</summary>
    public sealed record Ambiguous(IReadOnlyList<Found> Candidates) : SearchPlaceResult;
}
