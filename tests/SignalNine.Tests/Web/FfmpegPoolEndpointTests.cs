using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services.Ffmpeg;
using SignalNine.Tests.Support.Web;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class FfmpegPoolEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;
    private readonly StubFfmpegPool _stub = new();

    public FfmpegPoolEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _factory = new WebApplicationFactory<Program>()
                   .WithWebHostBuilder(
                       builder =>
                       {
                           builder.ConfigureServices(services =>
                           {
                               services.AddSingleton<IFfmpegPool>(_stub);
                           });
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
    public async Task Get_Processes_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/ffmpeg/processes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_Processes_Authenticated_ReturnsList()
    {
        _stub.Snapshots = new List<FfmpegProcessSnapshot>
        {
            new(Guid.NewGuid(), 1234, "ffmpeg", new[] { "-x" },
                FfmpegProcessStatusType.Running, DateTime.UtcNow, DateTime.UtcNow, null, null, null,
                Array.Empty<string>(), null)
        };

        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.GetAsync("/api/ffmpeg/processes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<FfmpegProcessSnapshot>>();
        Assert.Single(list!);
        Assert.Equal(1234, list![0].Pid);
    }

    [Fact]
    public async Task Get_Process_ById_ReturnsSnapshot()
    {
        var id = Guid.NewGuid();
        _stub.SnapshotById = new FfmpegProcessSnapshot(
            id, 555, "ffmpeg", new[] { "-x" },
            FfmpegProcessStatusType.Completed, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow,
            0, null, Array.Empty<string>(), null
        );

        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.GetAsync($"/api/ffmpeg/processes/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var snap = await response.Content.ReadFromJsonAsync<FfmpegProcessSnapshot>();
        Assert.Equal(id, snap!.Id);
    }

    [Fact]
    public async Task Get_Process_NotFound_Returns404()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.GetAsync($"/api/ffmpeg/processes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Cancel_Running_Returns204()
    {
        _stub.NextCancelResult = true;
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsync($"/api/ffmpeg/processes/{Guid.NewGuid()}/cancel", content: null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Post_Cancel_Terminal_Returns409()
    {
        var id = Guid.NewGuid();
        _stub.NextCancelResult = false;
        _stub.SnapshotById = new FfmpegProcessSnapshot(
            id, 1, "ffmpeg", new[] { "x" },
            FfmpegProcessStatusType.Completed, DateTime.UtcNow, DateTime.UtcNow, DateTime.UtcNow,
            0, null, Array.Empty<string>(), null
        );

        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsync($"/api/ffmpeg/processes/{id}/cancel", content: null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Post_Cancel_NotFound_Returns404()
    {
        _stub.NextCancelResult = false;
        _stub.SnapshotById = null;

        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsync($"/api/ffmpeg/processes/{Guid.NewGuid()}/cancel", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed class StubFfmpegPool : IFfmpegPool
    {
        public List<FfmpegProcessSnapshot> Snapshots { get; set; } = new();
        public FfmpegProcessSnapshot? SnapshotById { get; set; }
        public bool NextCancelResult { get; set; }

        public event EventHandler<FfmpegProcessSnapshot>? ProcessChanged;

        public Task<FfprobeResult> ProbeAsync(string inputPath, CancellationToken ct = default)
        {
            throw new NotSupportedException();
        }

        public Task<FfmpegProcessHandle> RunAsync(
            FfmpegInvocation invocation,
            IProgress<FfmpegProgressUpdate>? progress = null,
            CancellationToken ct = default
        )
        {
            throw new NotSupportedException();
        }

        public IReadOnlyList<FfmpegProcessSnapshot> List()
        {
            return Snapshots;
        }

        public FfmpegProcessSnapshot? Get(Guid id)
        {
            return SnapshotById;
        }

        public Task<bool> CancelAsync(Guid processId, CancellationToken ct = default)
        {
            return Task.FromResult(NextCancelResult);
        }
    }
}
