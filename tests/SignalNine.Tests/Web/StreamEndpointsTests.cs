using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Streaming;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Streaming;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class StreamEndpointsTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previous;

    public StreamEndpointsTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signal9-stream-ep-{Guid.NewGuid():N}");
        _previous = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);
        _factory = new WebApplicationFactory<Program>();
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _previous);
        if (Directory.Exists(_rootDirectory))
        {
            try { Directory.Delete(_rootDirectory, recursive: true); } catch { }
        }
        GC.SuppressFinalize(this);
    }

    private Guid SeedChannel()
    {
        using var scope = _factory.Services.CreateScope();
        var channels = scope.ServiceProvider.GetRequiredService<IDataAccess<ChannelEntity>>();
        var c = new ChannelEntity
        {
            Id = Guid.NewGuid(),
            Name = "T",
            Slug = $"t-{Guid.NewGuid():N}",
            IsActive = true
        };
        channels.Insert(c);
        return c.Id;
    }

    [Fact]
    public async Task PutEffects_SetsChannelVideoEffectsJson()
    {
        var channelId = SeedChannel();
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var body = new ChannelEffectsRequest(new[]
        {
            new ChannelEffect("vhs", true, new Dictionary<string, double> { ["intensity"] = 0.7 })
        });

        var resp = await client.PutAsJsonAsync($"/api/channels/{channelId}/effects", body);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var channels = scope.ServiceProvider.GetRequiredService<IDataAccess<ChannelEntity>>();
        var c = channels.GetByKey(channelId)!;
        Assert.Contains("\"vhs\"", c.VideoEffectsJson);
    }

    [Fact]
    public async Task GetCatalog_ReturnsTenEffects()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var resp = await client.GetAsync("/api/streaming/effects/catalog");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var items = await resp.Content.ReadFromJsonAsync<List<EffectCatalogItemResponse>>();
        Assert.NotNull(items);
        Assert.Equal(10, items!.Count);
        Assert.Contains(items, i => i.Kind == "vhs");
    }

    [Fact]
    public async Task GetStatus_BeforeFirstRequest_ReturnsNullSnapshot()
    {
        var channelId = SeedChannel();
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var resp = await client.GetAsync($"/api/channels/{channelId}/stream/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ChannelStreamSnapshot?>();
        Assert.Null(body);
    }

    [Fact]
    public async Task GetSegment_NoFile_Returns404()
    {
        var channelId = SeedChannel();
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var resp = await client.GetAsync($"/api/channels/{channelId}/stream/seg99999.ts");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
