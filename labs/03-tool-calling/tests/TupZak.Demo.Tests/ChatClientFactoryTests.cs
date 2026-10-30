using Microsoft.Extensions.Configuration;
using TupZak.Demo.Agent;

namespace TupZak.Demo.Tests;

/// <summary>Wybór dostawcy modelu na podstawie args. Testy nie łączą się z żadnym modelem.</summary>
public class ChatClientFactoryTests
{
    private static IConfiguration Configuration(string? apiKey = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:Model"] = "model-z-konfiguracji",
                ["MODEL_API_KEY"] = apiKey,
            })
            .Build();

    [Fact]
    public void Empty_args_select_ollama_with_model_from_configuration_and_no_api_key()
    {
        var factory = ChatClientFactory.Select([], Configuration());

        Assert.IsType<OllamaChatClientFactory>(factory);
        Assert.Equal("model-z-konfiguracji", factory.Model);
    }

    [Fact]
    public void Gemini_with_api_key_uses_default_model()
    {
        var factory = ChatClientFactory.Select(["--provider", "gemini"], Configuration(apiKey: "klucz"));

        Assert.IsType<GeminiChatClientFactory>(factory);
        Assert.Equal(new GeminiOptions().Model, factory.Model);
        Assert.Null(factory.Endpoint);
    }

    [Theory]
    [InlineData("ollama")]
    [InlineData("gemini")]
    [InlineData("GEMINI")]
    public void Model_argument_overrides_model_of_selected_provider(string provider)
    {
        var factory = ChatClientFactory.Select(["--provider", provider, "--model", "inny-model"], Configuration(apiKey: "klucz"));

        Assert.Equal("inny-model", factory.Model);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Gemini_without_api_key_is_rejected(string? apiKey)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ChatClientFactory.Select(["--provider", "gemini"], Configuration(apiKey)));

        Assert.Contains("MODEL_API_KEY", exception.Message);
    }

    [Fact]
    public void Unknown_provider_is_rejected()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ChatClientFactory.Select(["--provider", "nieznany"], Configuration()));

        Assert.Contains("nieznany", exception.Message);
    }

    [Theory]
    [InlineData("ollama")]
    [InlineData("gemini")]
    public void Create_returns_client_without_connecting_to_model(string provider)
    {
        var factory = ChatClientFactory.Select(["--provider", provider], Configuration(apiKey: "klucz"));

        using var client = factory.Create();

        Assert.NotNull(client);
    }
}
