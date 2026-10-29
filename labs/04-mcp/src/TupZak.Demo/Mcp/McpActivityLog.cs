using TupZak.Demo.Agent;

namespace TupZak.Demo.Mcp;

/// <summary>Zdarzenie wywołania narzędzia przez klienta MCP (np. Claude Code), z numerem kolejnym.</summary>
public sealed record McpActivity(long Sequence, string Client, AgentEvent Event);

/// <summary>
/// Dziennik wywołań narzędzi przez MCP. Przeglądarka odpytuje go cyklicznie,
/// żeby pokazać w panelu przebiegu, co robi zewnętrzny host (Claude Code).
/// </summary>
public sealed class McpActivityLog
{
    private const int Capacity = 200;

    private readonly Lock _lock = new();
    private readonly LinkedList<McpActivity> _items = new();
    private long _sequence;

    public McpActivity Add(string client, AgentEvent agentEvent)
    {
        lock (_lock)
        {
            var activity = new McpActivity(++_sequence, client, agentEvent);
            _items.AddLast(activity);
            if (_items.Count > Capacity)
                _items.RemoveFirst();
            return activity;
        }
    }

    public IReadOnlyList<McpActivity> After(long sequence)
    {
        lock (_lock) return _items.Where(a => a.Sequence > sequence).ToList();
    }

    public long LastSequence
    {
        get { lock (_lock) return _sequence; }
    }

    public void Clear()
    {
        lock (_lock) _items.Clear();
    }
}
