using System.Net;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using SignalNine.Tests.Support.Web;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class SwaggerEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public SwaggerEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _factory = new WebApplicationFactory<Program>()
                   .WithWebHostBuilder(
                       builder =>
                       {
                           builder.UseEnvironment("Development");
                       }
                   );
    }

    [Fact]
    public async Task Get_SwaggerIndex_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_SwaggerJson_ReturnsOk()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

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
