using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Services.Streaming;

namespace SignalNine.Tests.Web.Services.Streaming;

[Collection(WebApplicationCollection.Name)]
public class ChannelStreamCoordinatorTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previous;

    public ChannelStreamCoordinatorTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signal9-stream-coord-{Guid.NewGuid():N}");
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
            try { Directory.Delete(_rootDirectory, recursive: true); } catch { /* best effort */ }
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void GetOrStart_SameChannel_ReturnsSameInstance()
    {
        var coord = _factory.Services.GetRequiredService<ChannelStreamCoordinator>();
        var id = Guid.NewGuid();
        var first = coord.GetOrStart(id);
        var second = coord.GetOrStart(id);
        Assert.Same(first, second);
    }

    [Fact]
    public void OutputDir_IsBeneathRoot()
    {
        var coord = _factory.Services.GetRequiredService<ChannelStreamCoordinator>();
        var id = Guid.NewGuid();
        var dir = coord.OutputDir(id);
        Assert.StartsWith(_rootDirectory, dir);
        Assert.EndsWith(id.ToString(), dir);
    }
}
