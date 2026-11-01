namespace CampusAssistant.Domain.Features.SearchPlace;

/// <summary>UC-01: wyszukuje salę, budynek lub automat z kawą na kampusie i zwraca lokalizację.</summary>
public sealed class SearchPlaceUseCase(Campus campus)
{
    public SearchPlaceResult Execute(SearchPlaceQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentException.ThrowIfNullOrWhiteSpace(query.Phrase);

        string phrase = query.Phrase.Trim();
        var places = AllPlaces().ToList();

        // Dokładna nazwa ma pierwszeństwo, dzięki czemu student zawsze może wybrać kandydata ze scenariusza 3b.
        var exactMatches = places.Where(place => place.Name.Equals(phrase, StringComparison.OrdinalIgnoreCase)).ToList();
        var matches = exactMatches.Count > 0
            ? exactMatches
            : places.Where(place => place.Name.Contains(phrase, StringComparison.OrdinalIgnoreCase)).ToList();

        return matches switch
        {
            [] => new SearchPlaceResult.NotFound(),
            [var place] => ToFound(place),
            _ => new SearchPlaceResult.Ambiguous(matches.Select(ToFound).ToList()),
        };
    }

    private IEnumerable<Place> AllPlaces() =>
        campus.Buildings
            .Concat<Place>(campus.Buildings.SelectMany(building => building.Rooms))
            .Concat(campus.CoffeeMachines);

    private static SearchPlaceResult.Found ToFound(Place place) =>
        new(place.Name, place.Location.Description);
}
