using System.Text.Json;
using Microsoft.Extensions.AI;
using OllamaSharp;
using TupZak.Demo.Agent;
using TupZak.Demo.Services;
using TupZak.Demo.Tools;

var builder = WebApplication.CreateBuilder(args);

var ollamaOptions = builder.Configuration.GetSection("Ollama").Get<OllamaOptions>() ?? new OllamaOptions();
var agentOptions = builder.Configuration.GetSection("Agent").Get<AgentOptions>() ?? new AgentOptions();
string dataDirectory = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["DataDirectory"] ?? "../../data"));

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Encoder = CampusTools.JsonOptions.Encoder);

builder.Services.AddSingleton(ollamaOptions);
builder.Services.AddSingleton(agentOptions);
builder.Services.AddSingleton(_ => BuildingRepository.LoadFromDirectory(dataDirectory));
builder.Services.AddSingleton<MapState>();
builder.Services.AddSingleton<CampusTools>();
builder.Services.AddSingleton(sp => new ToolExecutor(sp.GetRequiredService<CampusTools>().CreateAIFunctions()));
builder.Services.AddSingleton<AgentLoop>();
builder.Services.AddSingleton<AgentSession>();

// OllamaApiClient implementuje IChatClient z Microsoft.Extensions.AI.
// Zamiana dostawcy modelu sprowadza się do zarejestrowania innej implementacji IChatClient.
builder.Services.AddChatClient(_ => new OllamaApiClient(new Uri(ollamaOptions.Endpoint), ollamaOptions.Model));

var app = builder.Build();

// Wczytanie i walidacja danych GeoJSON przy starcie: błędne dane zatrzymują aplikację od razu.
var repository = app.Services.GetRequiredService<BuildingRepository>();
app.Logger.LogInformation("Wczytano budynki {Buildings} z {Directory}. Model: {Model} @ {Endpoint}",
    string.Join(", ", repository.All.Select(b => b.Id)), dataDirectory, ollamaOptions.Model, ollamaOptions.Endpoint);

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/info", (ToolExecutor tools) => new
{
    ollamaOptions.Model,
    ollamaOptions.Endpoint,
    agentOptions.MaxIterations,
    agentOptions.TimeoutSeconds,
    Tools = tools.Functions.Select(f => new { f.Name, f.Description, Schema = f.JsonSchema }),
});

app.MapGet("/api/map", (BuildingRepository buildings, MapState mapState) => new
{
    Buildings = buildings.All.Select(b => new { b.Id, b.Name, GeoJson = b.GeoJson }),
    State = mapState.Snapshot,
});

// Tura rozmowy. Zdarzenia Agent Loop są wysyłane na żywo jako NDJSON (jeden obiekt JSON w linii).
app.MapPost("/api/chat", async (ChatRequest request, AgentSession session, HttpContext http) =>
{
    if (string.IsNullOrWhiteSpace(request.Message))
    {
        http.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    http.Response.ContentType = "application/x-ndjson; charset=utf-8";
    http.Response.Headers.CacheControl = "no-cache";

    await session.RunTurnAsync(request.Message.Trim(), async agentEvent =>
    {
        await http.Response.WriteAsync(JsonSerializer.Serialize(agentEvent, CampusTools.JsonOptions) + "\n", http.RequestAborted);
        await http.Response.Body.FlushAsync(http.RequestAborted);
    }, http.RequestAborted);
});

app.MapPost("/api/reset", async (AgentSession session) =>
{
    await session.ResetAsync();
    return Results.NoContent();
});

app.Run();

public sealed record ChatRequest(string? Message);
