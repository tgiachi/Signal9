using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Channels;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class ChannelEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public ChannelEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _factory = new WebApplicationFactory<Program>();
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _previousRootDirectory);

        if (Directory.Exists(_rootDirectory))
        {
            try { Directory.Delete(_rootDirectory, recursive: true); } catch { /* best effort */ }
        }

        GC.SuppressFinalize(this);
    }

    private static CreateChannelRequest NewChannelRequest(string slug = "test-channel") =>
        new(
            Name: "Test Channel",
            Slug: slug,
            Description: "An integration-test channel",
            LogoUrl: null,
            DisplayOrder: 0,
            CommercialsEnabled: true,
            CommercialIntervalMinSeconds: 120,
            CommercialIntervalMaxSeconds: 300
        );

    [Fact]
    public async Task Post_Channel_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/channels", NewChannelRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Channel_Authenticated_CreatesAndReturnsCreated()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsJsonAsync("/api/channels", NewChannelRequest("created-channel"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChannelResponse>();
        Assert.NotNull(body);
        Assert.Equal("created-channel", body!.Slug);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task Post_Channel_DuplicateSlug_ReturnsConflict()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var first = await client.PostAsJsonAsync("/api/channels", NewChannelRequest("dup-slug"));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/channels", NewChannelRequest("dup-slug"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Get_Channels_ReturnsCreated()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        await client.PostAsJsonAsync("/api/channels", NewChannelRequest("list-a"));
        await client.PostAsJsonAsync("/api/channels", NewChannelRequest("list-b"));

        var response = await client.GetAsync("/api/channels");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<ChannelResponse>>();
        Assert.NotNull(list);
        Assert.True(list!.Count >= 2);
    }

    [Fact]
    public async Task Get_Channel_BySlug_ReturnsChannel()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        await client.PostAsJsonAsync("/api/channels", NewChannelRequest("slug-find"));

        var response = await client.GetAsync("/api/channels/by-slug/slug-find");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChannelResponse>();
        Assert.Equal("slug-find", body!.Slug);
    }

    [Fact]
    public async Task Put_Channel_UpdatesFields()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var created = await (await client.PostAsJsonAsync("/api/channels", NewChannelRequest("put-test")))
                                 .Content.ReadFromJsonAsync<ChannelResponse>();

        var update = new UpdateChannelRequest(
            Name: "Renamed",
            Slug: "put-test",
            Description: "updated",
            LogoUrl: "logos/put.png",
            DisplayOrder: 7,
            IsActive: false,
            CommercialsEnabled: false,
            CommercialIntervalMinSeconds: 60,
            CommercialIntervalMaxSeconds: 60
        );

        var response = await client.PutAsJsonAsync($"/api/channels/{created!.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ChannelResponse>();
        Assert.Equal("Renamed", body!.Name);
        Assert.False(body.IsActive);
        Assert.False(body.CommercialsEnabled);
    }

    [Fact]
    public async Task Delete_Channel_RemovesIt()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var created = await (await client.PostAsJsonAsync("/api/channels", NewChannelRequest("delete-me")))
                                 .Content.ReadFromJsonAsync<ChannelResponse>();

        var del = await client.DeleteAsync($"/api/channels/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await client.GetAsync($"/api/channels/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
