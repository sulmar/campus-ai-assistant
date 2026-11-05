namespace TupZak.Demo.Agent;

public sealed class OllamaOptions
{
    public string Endpoint { get; set; } = "http://localhost:11434";
    public string Model { get; set; } = "qwen3:8b";

    /// <summary>Tryb „thinking” dla modeli, które go obsługują (np. qwen3). Null = domyślne zachowanie modelu.</summary>
    public bool? Think { get; set; }
}

public sealed class AgentOptions
{
    /// <summary>Maksymalna liczba zapytań do modelu w jednej turze rozmowy.</summary>
    public int MaxIterations { get; set; } = 6;

    /// <summary>Limit czasu na całą turę (wszystkie iteracje razem).</summary>
    public int TimeoutSeconds { get; set; } = 180;

    public float Temperature { get; set; } = 0;
    public long Seed { get; set; } = 42;
}
