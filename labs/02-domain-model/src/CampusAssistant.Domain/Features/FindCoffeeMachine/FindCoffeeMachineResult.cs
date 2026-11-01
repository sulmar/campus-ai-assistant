namespace CampusAssistant.Domain.Features.FindCoffeeMachine;

/// <summary>Wynik UC-03. Pusta lista <paramref name="InService"/> oznacza scenariusz alternatywny 4a: brak czynnych automatów.</summary>
/// <param name="InService">Automaty czynne.</param>
/// <param name="UnknownAvailability">Scenariusz alternatywny 3a: automaty o nieznanym statusie, których nie wolno przedstawiać jako czynnych.</param>
public sealed record FindCoffeeMachineResult(
    IReadOnlyList<FindCoffeeMachineResult.Machine> InService,
    IReadOnlyList<FindCoffeeMachineResult.Machine> UnknownAvailability)
{
    public sealed record Machine(string Name, string Location);
}
