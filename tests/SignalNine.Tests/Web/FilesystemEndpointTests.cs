// tests/SignalNine.Tests/Web/FilesystemEndpointTests.cs
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Filesystem;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class FilesystemEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;
    private readonly string _scratchRoot;

    public FilesystemEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _scratchRoot = Path.Combine(Path.GetTempPath(), $"signalnine-fs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_scratchRoot);

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
        if (Directory.Exists(_scratchRoot))
        {
            try { Directory.Delete(_scratchRoot, recursive: true); } catch { /* best effort */ }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Browse_Unauthenticated_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/fs/browse?path={Uri.EscapeDataString(_scratchRoot)}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Browse_ReturnsDirectoryContents_OrderedAndClassified()
    {
        Directory.CreateDirectory(Path.Combine(_scratchRoot, "music"));
        Directory.CreateDirectory(Path.Combine(_scratchRoot, "Movies"));
        File.WriteAllText(Path.Combine(_scratchRoot, "README"), "hi");
        File.WriteAllText(Path.Combine(_scratchRoot, ".hidden"), "shh");

        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.GetAsync($"/api/fs/browse?path={Uri.EscapeDataString(_scratchRoot)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FsBrowseResponse>();
        Assert.NotNull(body);
        Assert.Equal(_scratchRoot, body!.Path);
        Assert.Equal(Path.GetDirectoryName(_scratchRoot), body.Parent);

        // Directories first (case-insensitive alpha), then files (case-insensitive alpha).
        var names = body.Entries.Select(e => e.Name).ToList();
        Assert.Equal(new[] { "Movies", "music", ".hidden", "README" }, names);

        Assert.True(body.Entries.First(e => e.Name == "Movies").IsDirectory);
        Assert.False(body.Entries.First(e => e.Name == "README").IsDirectory);
    }

    [Fact]
    public async Task Browse_MissingPath_Returns404()
    {
        var missing = Path.Combine(_scratchRoot, "nope");
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.GetAsync($"/api/fs/browse?path={Uri.EscapeDataString(missing)}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Browse_RelativePath_Returns400()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.GetAsync($"/api/fs/browse?path={Uri.EscapeDataString("relative/dir")}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Browse_NormalizesDotSegments()
    {
        Directory.CreateDirectory(Path.Combine(_scratchRoot, "a"));
        Directory.CreateDirectory(Path.Combine(_scratchRoot, "b"));
        var weird = Path.Combine(_scratchRoot, "a", "..", "b");

        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.GetAsync($"/api/fs/browse?path={Uri.EscapeDataString(weird)}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<FsBrowseResponse>();
        Assert.NotNull(body);
        Assert.Equal(Path.Combine(_scratchRoot, "b"), body!.Path);
    }
}
