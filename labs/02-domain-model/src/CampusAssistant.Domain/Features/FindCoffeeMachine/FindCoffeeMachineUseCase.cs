namespace CampusAssistant.Domain.Features.FindCoffeeMachine;

/// <summary>UC-03: wyszukuje czynne automaty z kawą i osobno wskazuje automaty o nieznanej dostępności.</summary>
public sealed class FindCoffeeMachineUseCase(Campus campus)
{
    public FindCoffeeMachineResult Execute() =>
        new(WithAvailability(Availability.InService), WithAvailability(Availability.Unknown));

    private List<FindCoffeeMachineResult.Machine> WithAvailability(Availability availability) =>
        campus.CoffeeMachines
            .Where(coffeeMachine => coffeeMachine.Availability == availability)
            .Select(coffeeMachine => new FindCoffeeMachineResult.Machine(coffeeMachine.Name, coffeeMachine.Location.Description))
            .ToList();
}
