namespace CampusAssistant.Domain.Tests;

public class DomainModelTests
{
    private static readonly Location Gate = new("Main gate");
    private static readonly Location Hall = new("Ground floor, main hall");

    [Fact]
    public void LocationEquals_SameDescription_ReturnsTrue()
    {
        Assert.Equal(new Location("Main gate"), Gate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void LocationConstructor_BlankDescription_ThrowsArgumentException(string description)
    {
        Assert.Throws<ArgumentException>(() => new Location(description));
    }

    [Fact]
    public void Name_AnyPlaceType_ReturnsNameGivenAtCreation()
    {
        var campus = new Campus();
        var building = campus.AddBuilding("Building A", Gate);

        Place[] places =
        [
            building,
            building.AddRoom("A101", Hall),
            campus.AddCoffeeMachine("Hall machine", Hall),
        ];

        Assert.Equal(["Building A", "A101", "Hall machine"], places.Select(place => place.Name));
    }

    [Fact]
    public void Rooms_NewBuilding_IsEmpty()
    {
        var building = new Campus().AddBuilding("Building A", Gate);

        Assert.Empty(building.Rooms);
    }

    [Fact]
    public void AddRoom_ValidArguments_LinksRoomWithBuilding()
    {
        var building = new Campus().AddBuilding("Building A", Gate);

        var room = building.AddRoom("A101", Hall);

        Assert.Same(building, room.Building);
        Assert.Equal([room], building.Rooms);
    }

    [Fact]
    public void AddBuilding_ValidArguments_AddsBuildingToCampus()
    {
        var campus = new Campus();

        var building = campus.AddBuilding("Building A", Gate);

        Assert.Equal([building], campus.Buildings);
    }

    [Fact]
    public void AddCoffeeMachine_ValidArguments_AddsCoffeeMachineToCampus()
    {
        var campus = new Campus();

        var coffeeMachine = campus.AddCoffeeMachine("Hall machine", Hall);

        Assert.Equal([coffeeMachine], campus.CoffeeMachines);
    }

    [Fact]
    public void AddCoffeeMachine_NoBuilding_LeavesBuildingNull()
    {
        var coffeeMachine = new Campus().AddCoffeeMachine("Courtyard machine", Gate);

        Assert.Null(coffeeMachine.Building);
    }

    [Fact]
    public void AddCoffeeMachine_BuildingOfSameCampus_SetsBuilding()
    {
        var campus = new Campus();
        var building = campus.AddBuilding("Building A", Gate);

        var coffeeMachine = campus.AddCoffeeMachine("Hall machine", Hall, building: building);

        Assert.Same(building, coffeeMachine.Building);
    }

    [Fact]
    public void AddCoffeeMachine_BuildingOfAnotherCampus_ThrowsArgumentException()
    {
        var otherBuilding = new Campus().AddBuilding("Building X", Gate);

        Assert.Throws<ArgumentException>(
            () => new Campus().AddCoffeeMachine("Hall machine", Hall, building: otherBuilding));
    }

    [Fact]
    public void AddCoffeeMachine_NoAvailability_DefaultsToUnknown()
    {
        var coffeeMachine = new Campus().AddCoffeeMachine("Hall machine", Hall);

        Assert.Equal(Availability.Unknown, coffeeMachine.Availability);
    }

    [Fact]
    public void RouteConstructor_DifferentLocations_SetsStartDestinationAndDirections()
    {
        var route = new Route(Gate, Hall, ["Go straight", "Turn left"]);

        Assert.Equal(Gate, route.Start);
        Assert.Equal(Hall, route.Destination);
        Assert.Equal(["Go straight", "Turn left"], route.Directions);
    }

    [Fact]
    public void RouteConstructor_SameStartAndDestination_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new Route(Gate, new Location("Main gate"), []));
    }
}
