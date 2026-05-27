using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;
using SignalNine.Tests.Support.Web;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class HealthEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public HealthEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _factory = new WebApplicationFactory<Program>();
    }

    [Fact]
    public async Task Get_Live_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_Health_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
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
