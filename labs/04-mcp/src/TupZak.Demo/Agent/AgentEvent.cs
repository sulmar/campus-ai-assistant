using TupZak.Demo.Services;

namespace TupZak.Demo.Agent;

/// <summary>Zdarzenie przebiegu Agent Loop wysyłane na żywo do interfejsu.</summary>
public sealed record AgentEvent
{
    /// <summary>model | tool-call | tool-result | map | final | error</summary>
    public required string Type { get; init; }
    public int? Iteration { get; init; }
    public string? CallId { get; init; }
    public string? ToolName { get; init; }
    public string? Arguments { get; init; }
    public string? Result { get; init; }
    public string? Error { get; init; }
    public string? Text { get; init; }
    public string? Reasoning { get; init; }
    public int? ToolCallCount { get; init; }
    public double? DurationMs { get; init; }
    public long? InputTokens { get; init; }
    public long? OutputTokens { get; init; }
    public MapSnapshot? Map { get; init; }
}
