using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Extensions.Apps;
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
            [CampusTools.FindBuildingName, CampusTools.GetBuildingGeometryName, CampusTools.MarkEntranceName, CampusMapApp.ShowBuildingMapName],
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

        var buildings = Assert.Single(resources, r => r.Uri == CampusResources.BuildingsUri);
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

    [Theory]
    [InlineData("C", "C1", "C", "C1")]
    [InlineData("budynek a", "a3", "A", "A3")]
    public async Task Building_entrance_prompt_marks_entrance_directly_when_target_is_its_entrance(
        string name, string target, string buildingId, string entranceId)
    {
        var result = await _client.GetPromptAsync(CampusPrompts.BuildingEntranceName,
            new Dictionary<string, object?> { ["name"] = name, ["target"] = target });

        string text = Assert.IsType<TextContentBlock>(Assert.Single(result.Messages).Content).Text;
        Assert.Contains($"buildingId: \"{buildingId}\", entranceId: \"{entranceId}\"", text);
        Assert.DoesNotContain(CampusTools.GetBuildingGeometryName, text);
    }

    [Fact]
    public async Task Building_entrance_prompt_with_entrance_of_other_building_falls_back_to_search()
    {
        var result = await _client.GetPromptAsync(CampusPrompts.BuildingEntranceName,
            new Dictionary<string, object?> { ["name"] = "A", ["target"] = "B1" });

        string text = Assert.IsType<TextContentBlock>(Assert.Single(result.Messages).Content).Text;
        Assert.Contains(CampusTools.GetBuildingGeometryName, text);
        Assert.Contains("zapytaj mnie", text);
    }

    [Fact]
    public async Task Show_building_map_tool_is_linked_to_mcp_app_view()
    {
        var tool = (await _client.ListToolsAsync()).Single(t => t.Name == CampusMapApp.ShowBuildingMapName);

        var ui = tool.ProtocolTool.Meta!["ui"]!;
        Assert.Equal(CampusMapApp.MapViewUri, ui["resourceUri"]!.GetValue<string>());
        Assert.True(tool.ProtocolTool.Annotations?.ReadOnlyHint);
    }

    [Fact]
    public async Task Mcp_app_view_resource_is_html_with_mcp_app_profile()
    {
        var resources = await _client.ListResourcesAsync();
        Assert.Equal(McpApps.HtmlMimeType, Assert.Single(resources, r => r.Uri == CampusMapApp.MapViewUri).MimeType);

        var result = await _client.ReadResourceAsync(CampusMapApp.MapViewUri);

        var contents = Assert.IsType<TextResourceContents>(Assert.Single(result.Contents));
        Assert.Equal("text/html;profile=mcp-app", contents.MimeType);
        Assert.Contains("ui/initialize", contents.Text);
        Assert.Contains("appInfo:", contents.Text);  // pole wymagane przez spec MCP Apps (nie clientInfo)
        Assert.Contains("ui/notifications/tool-result", contents.Text);
    }

    [Fact]
    public async Task Show_building_map_returns_geojson_as_structured_content()
    {
        var result = await _client.CallToolAsync(CampusMapApp.ShowBuildingMapName,
            new Dictionary<string, object?> { ["buildingId"] = "A", ["entranceId"] = "a1" });

        Assert.NotEqual(true, result.IsError);
        var data = Assert.IsType<JsonElement>(result.StructuredContent);
        Assert.Equal("A", data.GetProperty("buildingId").GetString());
        Assert.Equal("A1", data.GetProperty("entranceId").GetString());
        Assert.Equal("FeatureCollection", data.GetProperty("geojson").GetProperty("type").GetString());
        Assert.Contains("wejściem A1", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Theory]
    [InlineData("D", null, "Dostępne budynki: A, B, C")]
    [InlineData("A", "B1", "nie należy do budynku A")]
    public async Task Show_building_map_rejects_unknown_building_or_entrance(string buildingId, string? entranceId, string error)
    {
        var result = await _client.CallToolAsync(CampusMapApp.ShowBuildingMapName,
            new Dictionary<string, object?> { ["buildingId"] = buildingId, ["entranceId"] = entranceId });

        Assert.True(result.IsError);
        Assert.Null(result.StructuredContent);
        Assert.Contains(error, Json(result).GetProperty("error").GetString());
    }

    private static JsonElement Json(CallToolResult result) =>
        JsonDocument.Parse(string.Concat(result.Content.OfType<TextContentBlock>().Select(c => c.Text))).RootElement;
}
