using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SignalNine.Tests.Support.Web;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class ConfigEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public ConfigEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _factory = new WebApplicationFactory<Program>();
    }

    [Fact]
    public async Task Get_Schema_ReturnsPipelineTaskConfigSchema()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/config/schema");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        var root = document.RootElement;
        Assert.Equal("https://json-schema.org/draft/2020-12/schema", root.GetProperty("$schema").GetString());

        var properties = root.GetProperty("properties");
        var pipeline = properties.GetProperty("Pipeline");
        var tasks = pipeline.GetProperty("properties").GetProperty("Tasks").GetProperty("properties");
        var probe = tasks.GetProperty("Probe");
        var preview = tasks.GetProperty("Preview");

        Assert.Equal("Probe", probe.GetProperty("title").GetString());
        Assert.Equal(
            "boolean",
            probe.GetProperty("properties").GetProperty("OverwriteExisting").GetProperty("type").GetString()
        );
        Assert.Equal(
            "boolean",
            probe.GetProperty("properties").GetProperty("AllowJellyfinStreamProbe").GetProperty("type").GetString()
        );
        Assert.Equal(
            "integer",
            preview.GetProperty("properties").GetProperty("PreviewCount").GetProperty("type").GetString()
        );
        Assert.Equal(
            "boolean",
            preview.GetProperty("properties").GetProperty("OverwriteExisting").GetProperty("type").GetString()
        );
        Assert.Equal(
            "boolean",
            preview.GetProperty("properties").GetProperty("AllowJellyfinStreamFallback").GetProperty("type").GetString()
        );
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _previousRootDirectory);

        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, true);
        }

        GC.SuppressFinalize(this);
    }
}
