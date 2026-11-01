using CampusAssistant.Domain.Features.SearchPlace;

namespace CampusAssistant.Domain.Tests.Features.SearchPlace;

public class SearchPlaceUseCaseTests
{
    private readonly Campus _campus = new();
    private readonly SearchPlaceUseCase _useCase;

    public SearchPlaceUseCaseTests()
    {
        var building = _campus.AddBuilding("Building A", new Location("Behind the main gate"));
        building.AddRoom("A101", new Location("Ground floor, first door on the left"));
        building.AddRoom("A102", new Location("Ground floor, second door on the left"));
        _campus.AddCoffeeMachine("Hall machine", new Location("Ground floor, main hall"));

        _useCase = new SearchPlaceUseCase(_campus);
    }

    [Theory]
    [InlineData("Building A", "Behind the main gate")]
    [InlineData("A101", "Ground floor, first door on the left")]
    [InlineData("Hall machine", "Ground floor, main hall")]
    public void Execute_NameOfAnyPlaceType_ReturnsFoundWithLocation(string phrase, string expectedLocation)
    {
        var result = _useCase.Execute(new SearchPlaceQuery(phrase));

        Assert.Equal(new SearchPlaceResult.Found(phrase, expectedLocation), result);
    }

    [Fact]
    public void Execute_DifferentCaseAndSurroundingSpaces_ReturnsFound()
    {
        var result = _useCase.Execute(new SearchPlaceQuery("  a101 "));

        Assert.Equal("A101", Assert.IsType<SearchPlaceResult.Found>(result).Name);
    }

    [Fact]
    public void Execute_PartOfSingleName_ReturnsFound()
    {
        var result = _useCase.Execute(new SearchPlaceQuery("machine"));

        Assert.Equal("Hall machine", Assert.IsType<SearchPlaceResult.Found>(result).Name);
    }

    [Fact]
    public void Execute_UnknownPhrase_ReturnsNotFound()
    {
        var result = _useCase.Execute(new SearchPlaceQuery("Z999"));

        Assert.IsType<SearchPlaceResult.NotFound>(result);
    }

    [Fact]
    public void Execute_PhraseMatchingSeveralPlaces_ReturnsAmbiguousWithAllCandidates()
    {
        var result = _useCase.Execute(new SearchPlaceQuery("A10"));

        var ambiguous = Assert.IsType<SearchPlaceResult.Ambiguous>(result);
        Assert.Equal(["A101", "A102"], ambiguous.Candidates.Select(candidate => candidate.Name));
    }

    [Fact]
    public void Execute_ExactNameContainedInAnotherName_ReturnsExactMatch()
    {
        _campus.Buildings[0].AddRoom("A1011", new Location("First floor"));

        var result = _useCase.Execute(new SearchPlaceQuery("A101"));

        Assert.Equal("A101", Assert.IsType<SearchPlaceResult.Found>(result).Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Execute_BlankPhrase_ThrowsArgumentException(string phrase)
    {
        Assert.Throws<ArgumentException>(() => _useCase.Execute(new SearchPlaceQuery(phrase)));
    }
}
