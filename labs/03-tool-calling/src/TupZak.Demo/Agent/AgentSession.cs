using Microsoft.Extensions.AI;
using TupZak.Demo.Services;

namespace TupZak.Demo.Agent;

/// <summary>
/// Jedna wspólna rozmowa demonstracyjna (bez logowania i wielu użytkowników).
/// Pilnuje, żeby w danej chwili trwała tylko jedna tura, i obsługuje timeout.
/// </summary>
public sealed class AgentSession(AgentLoop loop, MapState mapState, AgentOptions options)
{
    private readonly List<ChatMessage> _conversation = [];
    private readonly SemaphoreSlim _gate = new(1, 1);
    private CancellationTokenSource? _currentTurn;
    private bool _resetRequested;

    public async Task RunTurnAsync(string userMessage, Func<AgentEvent, Task> onEvent, CancellationToken requestAborted)
    {
        if (!await _gate.WaitAsync(0, requestAborted))
        {
            await onEvent(new AgentEvent { Type = "error", Error = "Agent jest zajęty poprzednim poleceniem." });
            return;
        }

        int historyLength = _conversation.Count;
        try
        {
            using var turn = CancellationTokenSource.CreateLinkedTokenSource(requestAborted);
            turn.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds));
            _currentTurn = turn;
            _resetRequested = false;

            _conversation.Add(new ChatMessage(ChatRole.User, userMessage));
            string reply = await loop.RunAsync(_conversation, onEvent, turn.Token);
            await onEvent(new AgentEvent { Type = "final", Text = reply, Map = mapState.Snapshot });
        }
        catch (Exception ex)
        {
            // Wycofujemy niedokończoną turę, żeby w historii nie zostały Tool Calls bez Tool Results.
            _conversation.RemoveRange(historyLength, _conversation.Count - historyLength);

            string error = ex switch
            {
                OperationCanceledException when _resetRequested => "Przerwano: rozmowa została wyczyszczona.",
                OperationCanceledException when !requestAborted.IsCancellationRequested =>
                    $"Przekroczono limit czasu ({options.TimeoutSeconds} s).",
                HttpRequestException => $"Brak połączenia z modelem: {ex.Message}",
                _ => ex.Message,
            };

            if (!requestAborted.IsCancellationRequested)
                await onEvent(new AgentEvent { Type = "error", Error = error, Map = mapState.Snapshot });
        }
        finally
        {
            _currentTurn = null;
            _gate.Release();
        }
    }

    /// <summary>Czyści rozmowę i mapę. Trwająca tura jest przerywana.</summary>
    public async Task ResetAsync()
    {
        _resetRequested = true;
        try { _currentTurn?.Cancel(); } catch (ObjectDisposedException) { }

        await _gate.WaitAsync();
        try
        {
            _conversation.Clear();
            mapState.Clear();
        }
        finally
        {
            _gate.Release();
        }
    }
}
