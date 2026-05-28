using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Interfaces;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Jellyfin;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class JellyfinEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;
    private readonly StubJellyfinService _stubJellyfin = new();

    public JellyfinEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _factory = new WebApplicationFactory<Program>()
                   .WithWebHostBuilder(
                       builder =>
                       {
                           builder.ConfigureServices(
                               services =>
                               {
                                   services.AddSingleton<IJellyfinService>(_stubJellyfin);
                               }
                           );
                       }
                   );
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

    [Fact]
    public async Task Put_Connection_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync(
            "/api/jellyfin/connection",
            new SetJellyfinConnectionRequest("https://jf.local", "k")
        );
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Put_Then_Get_Connection_Roundtrips_Status_WithoutKey()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var put = await client.PutAsJsonAsync(
            "/api/jellyfin/connection",
            new SetJellyfinConnectionRequest("https://jf.local", "secret-tok")
        );
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var get = await client.GetAsync("/api/jellyfin/connection");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var status = await get.Content.ReadFromJsonAsync<JellyfinConnectionStatus>();
        Assert.True(status!.IsConfigured);
        Assert.Equal("https://jf.local", status.BaseUrl);
    }

    [Fact]
    public async Task Put_Connection_InvalidUrl_ReturnsBadRequest()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var put = await client.PutAsJsonAsync(
            "/api/jellyfin/connection",
            new SetJellyfinConnectionRequest("not-a-url", "k")
        );
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Delete_Connection_Wipes()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        await client.PutAsJsonAsync(
            "/api/jellyfin/connection",
            new SetJellyfinConnectionRequest("https://jf.local", "k")
        );

        var del = await client.DeleteAsync("/api/jellyfin/connection");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await client.GetAsync("/api/jellyfin/connection");
        var status = await get.Content.ReadFromJsonAsync<JellyfinConnectionStatus>();
        Assert.False(status!.IsConfigured);
    }

    [Fact]
    public async Task Post_Test_NotConfigured_ReturnsConflict()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        _stubJellyfin.NextServerInfoException = new JellyfinNotConfiguredException();

        var response = await client.PostAsync("/api/jellyfin/test", content: null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Post_Test_AuthFailure_Returns401()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        _stubJellyfin.NextServerInfoException = new JellyfinAuthException("bad key");

        var response = await client.PostAsync("/api/jellyfin/test", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Test_Unreachable_Returns502()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        _stubJellyfin.NextServerInfoException = new JellyfinUnreachableException("down", 503);

        var response = await client.PostAsync("/api/jellyfin/test", content: null);
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task Post_Test_Success_Returns200AndMarksVerified()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        await client.PutAsJsonAsync(
            "/api/jellyfin/connection",
            new SetJellyfinConnectionRequest("https://jf.local", "k")
        );
        _stubJellyfin.NextServerInfo = new JellyfinServerInfo("Home", "10.9", "abc");

        var response = await client.PostAsync("/api/jellyfin/test", content: null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var info = await response.Content.ReadFromJsonAsync<JellyfinServerInfo>();
        Assert.Equal("Home", info!.ServerName);

        var get = await client.GetAsync("/api/jellyfin/connection");
        var status = await get.Content.ReadFromJsonAsync<JellyfinConnectionStatus>();
        Assert.NotNull(status!.LastVerifiedAt);
    }

    [Fact]
    public async Task Get_Libraries_Returns_StubbedList()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        _stubJellyfin.NextLibraries = new List<JellyfinLibrarySummary>
        {
            new("lib-1", "Movies", "movies"),
            new("lib-2", "TV", "tvshows")
        };

        var response = await client.GetAsync("/api/jellyfin/libraries");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<JellyfinLibrarySummary>>();
        Assert.Equal(2, list!.Count);
        Assert.Equal("Movies", list[0].Name);
    }

    private sealed class StubJellyfinService : IJellyfinService
    {
        public JellyfinServerInfo? NextServerInfo { get; set; }
        public Exception? NextServerInfoException { get; set; }
        public List<JellyfinLibrarySummary>? NextLibraries { get; set; }
        public Exception? NextLibrariesException { get; set; }

        public Task<JellyfinServerInfo> GetServerInfoAsync(CancellationToken ct = default)
        {
            if (NextServerInfoException is not null) throw NextServerInfoException;
            return Task.FromResult(NextServerInfo ?? new JellyfinServerInfo("S", "1", "x"));
        }

        public Task<IReadOnlyList<JellyfinLibrarySummary>> ListLibrariesAsync(CancellationToken ct = default)
        {
            if (NextLibrariesException is not null) throw NextLibrariesException;
            return Task.FromResult<IReadOnlyList<JellyfinLibrarySummary>>(
                NextLibraries ?? new List<JellyfinLibrarySummary>()
            );
        }

        public Task<JellyfinItem?> GetItemAsync(string itemId, CancellationToken ct = default)
        {
            return Task.FromResult<JellyfinItem?>(null);
        }

        public Task<IReadOnlyList<JellyfinItem>> ListItemsAsync(string libraryId, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<JellyfinItem>>(Array.Empty<JellyfinItem>());
        }

        public Task<IReadOnlyList<JellyfinPreviewImage>> GetPreviewImagesAsync(string itemId, int maxImages, CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<JellyfinPreviewImage>>(Array.Empty<JellyfinPreviewImage>());
        }

        public Task<JellyfinItemTags> GetItemTagsAsync(string itemId, CancellationToken ct = default)
        {
            return Task.FromResult(new JellyfinItemTags(Array.Empty<string>(), Array.Empty<string>()));
        }
    }
}
