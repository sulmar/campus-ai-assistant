using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using TupZak.Demo.Mcp;
using TupZak.Demo.Services;
using TupZak.Demo.Tools;

namespace TupZak.Demo.Tests;

/// <summary>
/// Testy serwera MCP przez prawdziwy transport Streamable HTTP (/mcp), w roli klienta MCP
/// takiego jak Claude Code. Nie wymagają modelu: wywołania narzędzi wysyła test.
/// </summary>
public sealed class McpServerTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private McpClient _client = null!;

    public McpServerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(_factory.Server.BaseAddress, "/mcp"), TransportMode = HttpTransportMode.StreamableHttp },
            _factory.CreateClient());
        _client = await McpClient.CreateAsync(transport);

        _factory.Services.GetRequiredService<MapState>().Clear();
        _factory.Services.GetRequiredService<McpActivityLog>().Clear();
    }

    public async Task DisposeAsync() => await _client.DisposeAsync();

    [Fact]
    public async Task Server_exposes_campus_tools_with_schemas_and_instructions()
    {
        var tools = await _client.ListToolsAsync();

        Assert.Equal(
            [CampusTools.FindBuildingName, CampusTools.GetBuildingGeometryName, CampusTools.MarkEntranceName],
            tools.Select(t => t.Name).Order());

        var markEntrance = tools.Single(t => t.Name == CampusTools.MarkEntranceName);
        var required = markEntrance.JsonSchema.GetProperty("required").EnumerateArray().Select(r => r.GetString());
        Assert.Equal(["buildingId", "entranceId"], required.Order());

        Assert.Equal("tupzak", _client.ServerInfo?.Name);
        Assert.Contains("mark_entrance", _client.ServerInstructions ?? "");
    }

    [Fact]
    public async Task Find_building_returns_building_id()
    {
        var result = await _client.CallToolAsync(CampusTools.FindBuildingName, new Dictionary<string, object?> { ["query"] = "budynek A" });

        var json = Json(result);
        Assert.NotEqual(true, result.IsError);
        Assert.True(json.GetProperty("found").GetBoolean());
        Assert.Equal("A", json.GetProperty("buildingId").GetString());
    }

    [Fact]
    public async Task Mark_entrance_updates_map_state_shared_with_browser()
    {
        await _client.CallToolAsync(CampusTools.MarkEntranceName,
            new Dictionary<string, object?> { ["buildingId"] = "A", ["entranceId"] = "A1" });

        var snapshot = _factory.Services.GetRequiredService<MapState>().Snapshot;
        Assert.Equal("A", snapshot.MarkedBuildingId);
        Assert.Equal("A1", snapshot.MarkedEntranceId);
    }

    [Fact]
    public async Task Mark_entrance_of_other_building_returns_domain_error()
    {
        var result = await _client.CallToolAsync(CampusTools.MarkEntranceName,
            new Dictionary<string, object?> { ["buildingId"] = "A", ["entranceId"] = "B1" });

        var json = Json(result);
        Assert.False(json.GetProperty("success").GetBoolean());
        Assert.Contains("należy do budynku B", json.GetProperty("error").GetString());
        Assert.Null(_factory.Services.GetRequiredService<MapState>().Snapshot.MarkedEntranceId);
    }

    [Fact]
    public async Task Empty_argument_is_rejected_by_the_same_validation_as_agent_loop()
    {
        var result = await _client.CallToolAsync(CampusTools.FindBuildingName, new Dictionary<string, object?> { ["query"] = "  " });

        Assert.True(result.IsError);
        Assert.Contains("nie może być pusty", Json(result).GetProperty("error").GetString());
    }

    [Fact]
    public async Task Tool_calls_are_recorded_in_activity_log_for_the_browser()
    {
        await _client.CallToolAsync(CampusTools.FindBuildingName, new Dictionary<string, object?> { ["query"] = "D" });

        var items = _factory.Services.GetRequiredService<McpActivityLog>().After(0);

        Assert.Equal(["tool-call", "tool-result"], items.Select(i => i.Event.Type));
        Assert.All(items, i => Assert.Equal(CampusTools.FindBuildingName, i.Event.ToolName));
        Assert.Equal(items[0].Event.CallId, items[1].Event.CallId);
        Assert.Contains("Nie znaleziono budynku", items[1].Event.Error);
    }

    [Fact]
    public async Task Server_exposes_building_list_resource_and_template()
    {
        var resources = await _client.ListResourcesAsync();
        var templates = await _client.ListResourceTemplatesAsync();

        var buildings = Assert.Single(resources);
        Assert.Equal(CampusResources.BuildingsUri, buildings.Uri);
        Assert.Equal("application/json", buildings.MimeType);
        Assert.Equal(CampusResources.BuildingUriTemplate, Assert.Single(templates).UriTemplate);
    }

    [Fact]
    public async Task Buildings_resource_lists_all_buildings_with_entrances()
    {
        var result = await _client.ReadResourceAsync(CampusResources.BuildingsUri);

        var json = JsonDocument.Parse(Assert.IsType<TextResourceContents>(Assert.Single(result.Contents)).Text).RootElement;
        Assert.Equal(["A", "B", "C"], json.EnumerateArray().Select(b => b.GetProperty("buildingId").GetString()));
        Assert.Contains("C2", json[2].GetProperty("entrances").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Building_resource_returns_geojson_of_building()
    {
        var result = await _client.ReadResourceAsync("tupzak://buildings/B");

        var contents = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("tupzak://buildings/B", contents.Uri);
        Assert.Equal("FeatureCollection", JsonDocument.Parse(contents.Text).RootElement.GetProperty("type").GetString());
    }

    [Fact]
    public async Task Unknown_building_resource_returns_resource_not_found()
    {
        var error = await Assert.ThrowsAsync<McpProtocolException>(() => _client.ReadResourceAsync("tupzak://buildings/D").AsTask());

        Assert.Equal(McpErrorCode.ResourceNotFound, error.ErrorCode);
        Assert.Contains("Dostępne budynki: A, B, C", error.Message);
    }

    [Fact]
    public async Task Server_exposes_building_entrance_prompt_with_arguments()
    {
        var prompt = Assert.Single(await _client.ListPromptsAsync());

        Assert.Equal(CampusPrompts.BuildingEntranceName, prompt.Name);
        Assert.Equal(["name", "target"], prompt.ProtocolPrompt.Arguments!.Select(a => a.Name).Order());
        Assert.All(prompt.ProtocolPrompt.Arguments!, a => Assert.True(a.Required));
    }

    [Fact]
    public async Task Building_entrance_prompt_fills_in_arguments()
    {
        var result = await _client.GetPromptAsync(CampusPrompts.BuildingEntranceName,
            new Dictionary<string, object?> { ["name"] = "Budynek A", ["target"] = "parking" });

        var message = Assert.Single(result.Messages);
        Assert.Equal(Role.User, message.Role);
        string text = Assert.IsType<TextContentBlock>(message.Content).Text;
        Assert.Contains("„Budynek A”", text);
        Assert.Contains("„parking”", text);
        Assert.Contains(CampusTools.MarkEntranceName, text);
    }

    private static JsonElement Json(CallToolResult result) =>
        JsonDocument.Parse(string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text))).RootElement;
}
