using Microsoft.Extensions.AI;
using TupZak.Demo.Agent;
using TupZak.Demo.Services;
using TupZak.Demo.Tools;

namespace TupZak.Demo.Tests;

/// <summary>
/// Testy mechaniki Agent Loop z zaprogramowanym klientem czatu.
/// Nie sprawdzają jakości rozumowania modelu — do tego potrzebny jest prawdziwy model.
/// </summary>
public class AgentLoopTests
{
    private readonly MapState _mapState = new();
    private readonly List<AgentEvent> _events = [];

    private AgentLoop CreateLoop(IChatClient client, int maxIterations = 6) =>
        new(client,
            new ToolExecutor(new CampusTools(TestData.LoadRepository(), _mapState).CreateAIFunctions()),
            _mapState,
            new AgentOptions { MaxIterations = maxIterations });

    private Task OnEvent(AgentEvent e)
    {
        _events.Add(e);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Multiple_tool_calls_in_one_response_are_executed_and_matched_by_call_id()
    {
        var client = new ScriptedChatClient(
            new ChatMessage(ChatRole.Assistant,
            [
                new FunctionCallContent("call-a", "find_building", new Dictionary<string, object?> { ["query"] = "A" }),
                new FunctionCallContent("call-d", "find_building", new Dictionary<string, object?> { ["query"] = "D" }),
            ]),
            new ChatMessage(ChatRole.Assistant, "Budynek A istnieje, budynku D nie ma."));

        var conversation = new List<ChatMessage> { new(ChatRole.User, "Znajdź budynki A i D") };
        string reply = await CreateLoop(client).RunAsync(conversation, OnEvent, CancellationToken.None);

        Assert.Equal("Budynek A istnieje, budynku D nie ma.", reply);

        // Drugie zapytanie do modelu zawiera wyniki obu narzędzi powiązane z identyfikatorami wywołań.
        var toolMessage = client.Requests[1].Single(m => m.Role == ChatRole.Tool);
        var results = toolMessage.Contents.OfType<FunctionResultContent>().ToList();
        Assert.Equal(["call-a", "call-d"], results.Select(r => r.CallId));

        Assert.Equal(["model", "tool-call", "tool-result", "tool-call", "tool-result", "map", "model"], _events.Select(e => e.Type));
    }

    [Fact]
    public async Task System_prompt_and_tool_definitions_are_sent_with_every_request()
    {
        var client = new ScriptedChatClient(new ChatMessage(ChatRole.Assistant, "Cześć!"));

        await CreateLoop(client).RunAsync([new(ChatRole.User, "Cześć")], OnEvent, CancellationToken.None);

        Assert.Equal(ChatRole.System, client.Requests[0][0].Role);
        Assert.Equal(["find_building", "get_building_geometry", "mark_entrance"], client.Options[0]!.Tools!.Select(t => t.Name));
    }

    [Fact]
    public async Task Answer_without_tool_calls_ends_the_loop_immediately()
    {
        var client = new ScriptedChatClient(new ChatMessage(ChatRole.Assistant, "Które wejście masz na myśli?"));

        string reply = await CreateLoop(client).RunAsync([new(ChatRole.User, "Zaznacz wejście z tyłu")], OnEvent, CancellationToken.None);

        Assert.Equal("Które wejście masz na myśli?", reply);
        Assert.Single(client.Requests);
        Assert.DoesNotContain(_events, e => e.Type == "tool-call");
    }

    [Fact]
    public async Task Invalid_tool_call_returns_error_to_model_instead_of_crashing()
    {
        var client = new ScriptedChatClient(
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "mark_entrance", new Dictionary<string, object?> { ["buildingId"] = "A", ["entranceId"] = "B1" })]),
            new ChatMessage(ChatRole.Assistant, "To wejście nie należy do budynku A."));

        await CreateLoop(client).RunAsync([new(ChatRole.User, "Zaznacz B1 w A")], OnEvent, CancellationToken.None);

        var result = _events.Single(e => e.Type == "tool-result");
        Assert.Contains("należy do budynku B", result.Result);
        Assert.Null(_mapState.Snapshot.MarkedEntranceId);
    }

    [Fact]
    public async Task Loop_stops_at_iteration_limit()
    {
        var call = new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-x", "find_building", new Dictionary<string, object?> { ["query"] = "A" })]);
        var client = new ScriptedChatClient(call, call, call, call);

        string reply = await CreateLoop(client, maxIterations: 3).RunAsync([new(ChatRole.User, "?")], OnEvent, CancellationToken.None);

        Assert.Contains("limitu 3 iteracji", reply);
        Assert.Equal(3, client.Requests.Count);
    }

    private sealed class ScriptedChatClient(params ChatMessage[] responses) : IChatClient
    {
        private int _next;
        public List<List<ChatMessage>> Requests { get; } = [];
        public List<ChatOptions?> Options { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToList());
            Options.Add(options);
            return Task.FromResult(new ChatResponse(responses[_next++]));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
