using CampusAssistant.Domain.Features.FindCoffeeMachine;

namespace CampusAssistant.Domain.Tests.Features.FindCoffeeMachine;

public class FindCoffeeMachineUseCaseTests
{
    private static readonly Location Hall = new("Ground floor, main hall");

    private readonly Campus _campus = new();
    private readonly FindCoffeeMachineUseCase _useCase;

    public FindCoffeeMachineUseCaseTests()
    {
        _useCase = new FindCoffeeMachineUseCase(_campus);
    }

    [Fact]
    public void Execute_InServiceMachine_ReturnsItsNameAndLocationAsInService()
    {
        _campus.AddCoffeeMachine("Hall machine", Hall, Availability.InService);

        var result = _useCase.Execute();

        Assert.Equal([new FindCoffeeMachineResult.Machine("Hall machine", Hall.Description)], result.InService);
        Assert.Empty(result.UnknownAvailability);
    }

    [Fact]
    public void Execute_UnknownAvailability_ReturnsMachineOnlyAsUnknown()
    {
        _campus.AddCoffeeMachine("Hall machine", Hall, Availability.Unknown);

        var result = _useCase.Execute();

        Assert.Empty(result.InService);
        Assert.Equal("Hall machine", Assert.Single(result.UnknownAvailability).Name);
    }

    [Fact]
    public void Execute_OutOfServiceMachine_OmitsMachineFromBothLists()
    {
        _campus.AddCoffeeMachine("Hall machine", Hall, Availability.OutOfService);

        var result = _useCase.Execute();

        Assert.Empty(result.InService);
        Assert.Empty(result.UnknownAvailability);
    }

    [Fact]
    public void Execute_NoCoffeeMachines_ReturnsEmptyLists()
    {
        var result = _useCase.Execute();

        Assert.Empty(result.InService);
        Assert.Empty(result.UnknownAvailability);
    }

    [Fact]
    public void Execute_MachinesWithMixedAvailability_GroupsThemByAvailability()
    {
        _campus.AddCoffeeMachine("Working 1", Hall, Availability.InService);
        _campus.AddCoffeeMachine("Broken", Hall, Availability.OutOfService);
        _campus.AddCoffeeMachine("Unchecked", Hall, Availability.Unknown);
        _campus.AddCoffeeMachine("Working 2", Hall, Availability.InService);

        var result = _useCase.Execute();

        Assert.Equal(["Working 1", "Working 2"], result.InService.Select(machine => machine.Name));
        Assert.Equal(["Unchecked"], result.UnknownAvailability.Select(machine => machine.Name));
    }

    [Fact]
    public void Execute_AvailabilityChangedAfterCreation_UsesCurrentAvailability()
    {
        var coffeeMachine = _campus.AddCoffeeMachine("Hall machine", Hall, Availability.InService);
        coffeeMachine.Availability = Availability.OutOfService;

        var result = _useCase.Execute();

        Assert.Empty(result.InService);
    }
}
