using Google.GenAI;
using Microsoft.Extensions.AI;
using OllamaSharp;

namespace TupZak.Demo.Agent;

/// <summary>Tworzy klienta czatu dla jednego dostawcy modelu razem z jego specyficznymi ustawieniami.</summary>
public interface IChatClientFactory
{
    string Model { get; }
    string? Endpoint { get; }
    IChatClient Create();
}

public sealed class OllamaChatClientFactory(OllamaOptions options, string? apiKey) : IChatClientFactory
{
    public string Model => options.Model;
    public string? Endpoint => options.Endpoint;

    public IChatClient Create()
    {
        var http = new HttpClient { BaseAddress = new Uri(options.Endpoint) };
        if (apiKey is not null)
            http.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);

        return new ChatClientBuilder(new OllamaApiClient(http, options.Model))
            .ConfigureOptions(chatOptions =>
            {
                if (options.Think is bool think)
                    (chatOptions.AdditionalProperties ??= [])["think"] = think;
            })
            .Build();
    }
}

public sealed class GeminiChatClientFactory(GeminiOptions options, string apiKey) : IChatClientFactory
{
    public string Model => options.Model;
    public string? Endpoint => null;

    // Reasoning.Output włącza zwracanie myśli modelu, które UI pokazuje jako „Rozumowanie modelu”.
    public IChatClient Create() =>
        new Client(apiKey: apiKey).AsIChatClient(options.Model).AsBuilder()
            .ConfigureOptions(chatOptions => chatOptions.Reasoning = new ReasoningOptions { Output = ReasoningOutput.Summary })
            .Build();
}

public static class ChatClientFactory
{
    /// <summary>
    /// Wybiera dostawcę na podstawie parametrów --provider i --model z wiersza poleceń.
    /// Parametry są czytane tylko z args, bo konfiguracja hosta zawiera też wszystkie zmienne środowiskowe.
    /// Klucz API pochodzi ze zmiennej MODEL_API_KEY, żeby nie trafiał do historii powłoki i listy procesów.
    /// </summary>
    public static IChatClientFactory Select(string[] args, IConfiguration configuration)
    {
        var commandLine = new ConfigurationBuilder().AddCommandLine(args).Build();
        string provider = commandLine["provider"] ?? "ollama";
        string? model = commandLine["model"];
        string? apiKey = configuration["MODEL_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = null;

        switch (provider.ToLowerInvariant())
        {
            case "ollama":
                var ollamaOptions = configuration.GetSection("Ollama").Get<OllamaOptions>() ?? new OllamaOptions();
                if (model is not null)
                    ollamaOptions.Model = model;
                return new OllamaChatClientFactory(ollamaOptions, apiKey);

            case "gemini":
                if (apiKey is null)
                    throw new InvalidOperationException("Dostawca gemini wymaga klucza API w zmiennej środowiskowej MODEL_API_KEY.");
                var geminiOptions = new GeminiOptions();
                if (model is not null)
                    geminiOptions.Model = model;
                return new GeminiChatClientFactory(geminiOptions, apiKey);

            default:
                throw new InvalidOperationException($"Nieznany dostawca modelu \"{provider}\". Dostępni dostawcy: ollama, gemini.");
        }
    }
}
