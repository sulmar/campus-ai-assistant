namespace CampusAssistant.Domain.Features.SearchPlace;

/// <param name="Phrase">Nazwa miejsca, którego szuka student, np. A101.</param>
public sealed record SearchPlaceQuery(string Phrase);
