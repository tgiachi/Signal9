using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SignalNine.Persistence.Types;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Channels;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class MediaLibraryEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public MediaLibraryEndpointTests()
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

    private static CreateMediaLibraryRequest NewRequest(string sourceRef = "jf-001")
    {
        return new(
            Name: "Home Movies",
            Description: "All my movies",
            DefaultMediaType: ChannelMediaType.Movies,
            SourceType: MediaSourceType.Jellyfin,
            SourceRef: sourceRef
        );
    }

    [Fact]
    public async Task Post_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/media-libraries", NewRequest());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Authenticated_CreatesAndReturnsCreated()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsJsonAsync("/api/media-libraries", NewRequest("post-1"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<MediaLibraryResponse>();
        Assert.NotNull(body);
        Assert.Equal("Home Movies", body!.Name);
        Assert.True(body.IsActive);
    }

    [Fact]
    public async Task Post_Duplicate_SourceTypeAndRef_ReturnsConflict()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        await client.PostAsJsonAsync("/api/media-libraries", NewRequest("dup-ref"));

        var second = await client.PostAsJsonAsync("/api/media-libraries", NewRequest("dup-ref"));
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Get_List_ReturnsCreated()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        await client.PostAsJsonAsync("/api/media-libraries", NewRequest("a"));
        await client.PostAsJsonAsync("/api/media-libraries", NewRequest("b"));

        var response = await client.GetAsync("/api/media-libraries");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<MediaLibraryResponse>>();
        Assert.True(list!.Count >= 2);
    }

    [Fact]
    public async Task Put_Updates()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var created = await (await client.PostAsJsonAsync("/api/media-libraries", NewRequest("put-ref")))
                                 .Content.ReadFromJsonAsync<MediaLibraryResponse>();

        var update = new UpdateMediaLibraryRequest(
            Name: "Renamed",
            Description: "new desc",
            DefaultMediaType: ChannelMediaType.Bumper,
            IsActive: false,
            SourceType: MediaSourceType.Jellyfin,
            SourceRef: "put-ref"
        );

        var response = await client.PutAsJsonAsync($"/api/media-libraries/{created!.Id}", update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MediaLibraryResponse>();
        Assert.Equal("Renamed", body!.Name);
        Assert.Equal(ChannelMediaType.Bumper, body.DefaultMediaType);
        Assert.False(body.IsActive);
    }

    [Fact]
    public async Task Delete_Removes()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var created = await (await client.PostAsJsonAsync("/api/media-libraries", NewRequest("del-ref")))
                                 .Content.ReadFromJsonAsync<MediaLibraryResponse>();

        var del = await client.DeleteAsync($"/api/media-libraries/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await client.GetAsync($"/api/media-libraries/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
