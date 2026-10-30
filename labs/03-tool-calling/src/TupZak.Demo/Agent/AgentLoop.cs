using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;
using TupZak.Demo.Services;
using TupZak.Demo.Tools;

namespace TupZak.Demo.Agent;

/// <summary>
/// Agent Loop napisany ręcznie, bez FunctionInvokingChatClient, żeby każdy krok był widoczny:
/// model → żądania Tool Call → walidacja i wykonanie → Tool Results → model → … → odpowiedź końcowa.
/// </summary>
public sealed class AgentLoop(
    IChatClient chatClient,
    ToolExecutor tools,
    MapState mapState,
    AgentOptions options)
{
    public async Task<string> RunAsync(
        IList<ChatMessage> conversation,
        Func<AgentEvent, Task> onEvent,
        CancellationToken cancellationToken)
    {
        var chatOptions = new ChatOptions
        {
            Tools = [.. tools.Functions],   // Tool Definitions wysyłane razem z każdym zapytaniem
            ToolMode = ChatToolMode.Auto,   // model sam decyduje, czy i które narzędzie wywołać
            Temperature = options.Temperature,
            Seed = options.Seed,
        };

        for (int iteration = 1; iteration <= options.MaxIterations; iteration++)
        {
            // 1. Wysłanie rozmowy i definicji narzędzi do modelu.
            var stopwatch = Stopwatch.StartNew();
            List<ChatMessage> messages = [new(ChatRole.System, SystemPrompt.Text), .. conversation];
            ChatResponse response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
            conversation.AddMessages(response);

            // 2. Odczytanie żądań Tool Call (może ich być kilka w jednej odpowiedzi).
            var calls = response.Messages
                .SelectMany(m => m.Contents)
                .OfType<FunctionCallContent>()
                .ToList();

            string reasoning = string.Concat(response.Messages.SelectMany(m => m.Contents).OfType<TextReasoningContent>().Select(r => r.Text));

            await onEvent(new AgentEvent
            {
                Type = "model",
                Iteration = iteration,
                ToolCallCount = calls.Count,
                Text = string.IsNullOrWhiteSpace(response.Text) ? null : response.Text,
                Reasoning = string.IsNullOrWhiteSpace(reasoning) ? null : reasoning.Trim(),
                DurationMs = stopwatch.Elapsed.TotalMilliseconds,
                InputTokens = response.Usage?.InputTokenCount,
                OutputTokens = response.Usage?.OutputTokenCount,
            });

            // Brak żądań narzędzi = odpowiedź końcowa.
            if (calls.Count == 0)
                return response.Text;

            // 3–4. Walidacja i wykonanie każdego wywołania przez aplikację.
            var results = new List<AIContent>();
            foreach (var call in calls)
            {
                await onEvent(new AgentEvent
                {
                    Type = "tool-call",
                    Iteration = iteration,
                    CallId = call.CallId,
                    ToolName = call.Name,
                    Arguments = JsonSerializer.Serialize(call.Arguments ?? new Dictionary<string, object?>(), CampusTools.JsonOptions),
                });

                var execution = await tools.ExecuteAsync(call, cancellationToken);

                // 5. Tool Result jest powiązany z Tool Call przez CallId.
                results.Add(new FunctionResultContent(call.CallId, execution.Result));

                await onEvent(new AgentEvent
                {
                    Type = "tool-result",
                    Iteration = iteration,
                    CallId = call.CallId,
                    ToolName = call.Name,
                    Result = JsonSerializer.Serialize(execution.Result, CampusTools.JsonOptions),
                    Error = execution.Error,
                    DurationMs = execution.DurationMs,
                });
            }

            conversation.Add(new ChatMessage(ChatRole.Tool, results));
            await onEvent(new AgentEvent { Type = "map", Map = mapState.Snapshot });

            // 6. Kolejna iteracja: model dostaje wyniki narzędzi i decyduje, co dalej.
        }

        return $"Przerwano po osiągnięciu limitu {options.MaxIterations} iteracji Agent Loop, zanim model udzielił odpowiedzi końcowej.";
    }
}
