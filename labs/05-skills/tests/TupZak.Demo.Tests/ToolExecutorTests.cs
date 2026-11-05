using System.Text.Json;
using Microsoft.Extensions.AI;
using TupZak.Demo.Agent;
using TupZak.Demo.Services;
using TupZak.Demo.Tools;

namespace TupZak.Demo.Tests;

public class ToolExecutorTests
{
    private readonly MapState _mapState = new();
    private readonly ToolExecutor _executor;

    public ToolExecutorTests()
    {
        _executor = new ToolExecutor(new CampusTools(TestData.LoadRepository(), _mapState).CreateAIFunctions());
    }

    private Task<ToolExecution> Execute(string name, Dictionary<string, object?>? arguments) =>
        _executor.ExecuteAsync(new FunctionCallContent("call-1", name, arguments), CancellationToken.None);

    [Fact]
    public async Task Valid_call_is_executed()
    {
        var execution = await Execute("mark_entrance", new() { ["buildingId"] = "A", ["entranceId"] = "A1" });

        Assert.Null(execution.Error);
        Assert.Equal("A1", _mapState.Snapshot.MarkedEntranceId);
    }

    [Fact]
    public async Task Arguments_as_JsonElements_are_accepted()
    {
        // Tak argumenty przychodzą od dostawcy modelu po deserializacji JSON.
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>("""{ "query": "B" }""");
        var execution = await Execute("find_building", args);

        Assert.Null(execution.Error);
        Assert.Contains("\"buildingId\":\"B\"", JsonSerializer.Serialize(execution.Result, CampusTools.JsonOptions));
    }

    [Fact]
    public async Task Domain_error_returned_by_tool_is_reported()
    {
        var execution = await Execute("mark_entrance", new() { ["buildingId"] = "A", ["entranceId"] = "B1" });

        Assert.Contains("należy do budynku B", execution.Error);
    }

    [Fact]
    public async Task Unknown_tool_is_rejected()
    {
        var execution = await Execute("delete_building", new() { ["buildingId"] = "A" });

        Assert.Contains("Nieznane narzędzie", execution.Error);
        Assert.IsType<ToolError>(execution.Result);
    }

    [Fact]
    public async Task Missing_required_argument_is_rejected_before_execution()
    {
        var execution = await Execute("mark_entrance", new() { ["buildingId"] = "A" });

        Assert.Contains("Brak wymaganego argumentu \"entranceId\"", execution.Error);
        Assert.Null(_mapState.Snapshot.MarkedEntranceId);
    }

    [Fact]
    public async Task Unexpected_argument_is_rejected()
    {
        // Model nie może przekazać współrzędnych — tylko identyfikatory.
        var execution = await Execute("mark_entrance", new() { ["buildingId"] = "A", ["entranceId"] = "A1", ["x"] = 3.5 });

        Assert.Contains("nie przyjmuje argumentów: x", execution.Error);
        Assert.Null(_mapState.Snapshot.MarkedEntranceId);
    }

    [Fact]
    public async Task Empty_string_argument_is_rejected()
    {
        var execution = await Execute("get_building_geometry", new() { ["buildingId"] = "  " });
        Assert.Contains("nie może być pusty", execution.Error);
    }

    [Fact]
    public async Task Non_string_argument_is_rejected()
    {
        var args = JsonSerializer.Deserialize<Dictionary<string, object?>>("""{ "buildingId": 7 }""");
        var execution = await Execute("get_building_geometry", args);

        Assert.Contains("musi być tekstem", execution.Error);
    }

    [Fact]
    public async Task Argument_parsing_error_is_reported()
    {
        var call = new FunctionCallContent("call-1", "find_building") { Exception = new JsonException("Unexpected token") };
        var execution = await _executor.ExecuteAsync(call, CancellationToken.None);

        Assert.Contains("Nie udało się odczytać argumentów JSON", execution.Error);
    }

    [Fact]
    public async Task Exception_thrown_by_tool_is_converted_to_error_result()
    {
        var failing = AIFunctionFactory.Create(int () => throw new InvalidOperationException("awaria"), "failing");
        var executor = new ToolExecutor([failing]);

        var execution = await executor.ExecuteAsync(new FunctionCallContent("call-1", "failing"), CancellationToken.None);

        Assert.Contains("awaria", execution.Error);
    }
}
