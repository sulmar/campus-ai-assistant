using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace TupZak.Demo.Agent;

/// <summary>Wynik błędu przekazywany modelowi zamiast wyniku narzędzia.</summary>
public sealed record ToolError(string Error);

public sealed record ToolExecution(object? Result, string? Error, double DurationMs);

/// <summary>
/// Wykonuje wywołania narzędzi zaproponowane przez model.
/// Model niczego nie uruchamia sam — to tutaj aplikacja sprawdza nazwę narzędzia
/// i argumenty, a dopiero potem wywołuje metodę C#.
/// </summary>
public sealed class ToolExecutor(IEnumerable<AIFunction> functions)
{
    private readonly Dictionary<string, AIFunction> _functions = functions.ToDictionary(f => f.Name);

    public IReadOnlyCollection<AIFunction> Functions => _functions.Values;

    public async Task<ToolExecution> ExecuteAsync(FunctionCallContent call, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        ToolExecution Fail(string error) => new(new ToolError(error), error, stopwatch.Elapsed.TotalMilliseconds);

        // 1. Czy model wybrał istniejące narzędzie?
        if (!_functions.TryGetValue(call.Name, out var function))
            return Fail($"Nieznane narzędzie \"{call.Name}\". Dostępne narzędzia: {string.Join(", ", _functions.Keys)}.");

        // 2. Czy argumenty dało się odczytać jako JSON?
        if (call.Exception is not null)
            return Fail($"Nie udało się odczytać argumentów JSON: {call.Exception.Message}");

        // 3. Czy argumenty są zgodne z JSON Schema narzędzia?
        string? validationError = ValidateArguments(function, call.Arguments);
        if (validationError is not null)
            return Fail(validationError);

        // 4. Dopiero teraz aplikacja wykonuje funkcję C#.
        try
        {
            object? result = await function.InvokeAsync(new AIFunctionArguments(call.Arguments), cancellationToken);

            // Narzędzie może zakończyć się poprawnie, ale zgłosić błąd domenowy (np. nieistniejące wejście).
            string? domainError = result is JsonElement { ValueKind: JsonValueKind.Object } json &&
                                  json.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String
                ? error.GetString()
                : null;

            return new ToolExecution(result, domainError, stopwatch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail($"Błąd wykonania narzędzia {call.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Sprawdza argumenty względem JSON Schema narzędzia: nieznane argumenty,
    /// brakujące argumenty wymagane i puste lub nietekstowe wartości dla pól typu string.
    /// </summary>
    public static string? ValidateArguments(AIFunction function, IDictionary<string, object?>? arguments)
    {
        arguments ??= new Dictionary<string, object?>();
        var schema = function.JsonSchema;

        var properties = schema.TryGetProperty("properties", out var p) && p.ValueKind == JsonValueKind.Object
            ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value)
            : [];

        var unknown = arguments.Keys.Where(k => !properties.ContainsKey(k)).ToList();
        if (unknown.Count > 0)
            return $"Narzędzie {function.Name} nie przyjmuje argumentów: {string.Join(", ", unknown)}. " +
                   $"Oczekiwane argumenty: {string.Join(", ", properties.Keys)}.";

        if (schema.TryGetProperty("required", out var required) && required.ValueKind == JsonValueKind.Array)
        {
            foreach (var name in required.EnumerateArray().Select(r => r.GetString()!))
            {
                if (!arguments.TryGetValue(name, out var value) || value is null || value is JsonElement { ValueKind: JsonValueKind.Null })
                    return $"Brak wymaganego argumentu \"{name}\" w wywołaniu {function.Name}.";
            }
        }

        foreach (var (name, value) in arguments)
        {
            if (!properties[name].TryGetProperty("type", out var type) || type.GetString() != "string")
                continue;

            string? text = value switch
            {
                string s => s,
                JsonElement { ValueKind: JsonValueKind.String } e => e.GetString(),
                _ => null,
            };

            if (text is null)
                return $"Argument \"{name}\" w wywołaniu {function.Name} musi być tekstem.";
            if (string.IsNullOrWhiteSpace(text))
                return $"Argument \"{name}\" w wywołaniu {function.Name} nie może być pusty.";
        }

        return null;
    }
}
