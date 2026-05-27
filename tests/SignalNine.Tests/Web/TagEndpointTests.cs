using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Channels;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class TagEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public TagEndpointTests()
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

    [Fact]
    public async Task Post_Tag_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("action", "Action"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Tag_NormalizesNameLowercase()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Action", "Action"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<TagResponse>();
        Assert.Equal("action", body!.Name);
    }

    [Fact]
    public async Task Post_Tag_DuplicateName_ReturnsExisting()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var first = await (await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("Sport", null)))
                                .Content.ReadFromJsonAsync<TagResponse>();
        var second = await (await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("sport", null)))
                                 .Content.ReadFromJsonAsync<TagResponse>();

        Assert.Equal(first!.Id, second!.Id);
    }

    [Fact]
    public async Task Get_Tags_ReturnsCreated()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("news", null));

        var response = await client.GetAsync("/api/tags");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<TagResponse>>();
        Assert.NotNull(list);
        Assert.Contains(list!, t => t.Name == "news");
    }

    [Fact]
    public async Task Delete_Tag_RemovesIt()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var created = await (await client.PostAsJsonAsync("/api/tags", new CreateTagRequest("temporary", null)))
                                 .Content.ReadFromJsonAsync<TagResponse>();

        var del = await client.DeleteAsync($"/api/tags/{created!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
    }
}
