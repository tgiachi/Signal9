using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Workers;
using SignalNine.Core.Interfaces;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Workers;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class WorkerEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;

    public WorkerEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _previousRootDirectory);

        if (Directory.Exists(_rootDirectory))
        {
            try { Directory.Delete(_rootDirectory, recursive: true); } catch { /* best effort */ }
        }

        GC.SuppressFinalize(this);
    }

    // -------------------------------------------------------------------------
    // Test 1: Unauthenticated → 401
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_Workers_Unauthenticated_Returns401()
    {
        using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/workers");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // -------------------------------------------------------------------------
    // Test 2: Authenticated, empty registry → 200 with empty array
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_Workers_Authenticated_EmptyRegistry_Returns200WithEmptyList()
    {
        var stub = new WkStubWorkerRegistry(Array.Empty<WorkerInfo>());

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IWorkerRegistry>(stub);
                });
            });

        using var client = JwtClientFactory.CreateAuthorizedClient(factory.CreateClient());

        var response = await client.GetAsync("/api/workers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<WorkerResponse>>();
        Assert.NotNull(list);
        Assert.Empty(list);
    }

    // -------------------------------------------------------------------------
    // Test 3: Authenticated, 2 workers → 200 with 2 entries, correct fields
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_Workers_Authenticated_TwoWorkers_Returns200WithCorrectFields()
    {
        var workerA = new WorkerInfo(
            WorkerId: Guid.NewGuid(),
            Name: "worker-alpha",
            Version: "2.0.0.0",
            RunningJobs: 1,
            MaxConcurrentJobs: 4,
            CurrentJobIds: new[] { Guid.NewGuid() },
            LastSeenAt: DateTimeOffset.UtcNow.AddSeconds(-5),
            Online: true
        );
        var workerB = new WorkerInfo(
            WorkerId: Guid.NewGuid(),
            Name: "worker-beta",
            Version: "2.1.0.0",
            RunningJobs: 0,
            MaxConcurrentJobs: 2,
            CurrentJobIds: Array.Empty<Guid>(),
            LastSeenAt: DateTimeOffset.UtcNow.AddSeconds(-60),
            Online: false
        );

        var stub = new WkStubWorkerRegistry(new[] { workerA, workerB });

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IWorkerRegistry>(stub);
                });
            });

        using var client = JwtClientFactory.CreateAuthorizedClient(factory.CreateClient());

        var response = await client.GetAsync("/api/workers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var list = await response.Content.ReadFromJsonAsync<List<WorkerResponse>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);

        var alpha = list.Single(w => w.WorkerId == workerA.WorkerId);
        Assert.Equal(workerA.Name, alpha.Name);
        Assert.Equal(workerA.Version, alpha.Version);
        Assert.Equal(workerA.RunningJobs, alpha.RunningJobs);
        Assert.Equal(workerA.MaxConcurrentJobs, alpha.MaxConcurrentJobs);
        Assert.Single(alpha.CurrentJobIds);
        Assert.True(alpha.Online);

        var beta = list.Single(w => w.WorkerId == workerB.WorkerId);
        Assert.Equal(workerB.Name, beta.Name);
        Assert.Empty(beta.CurrentJobIds);
        Assert.False(beta.Online);
    }
}

// -------------------------------------------------------------------------
// Stub
// -------------------------------------------------------------------------

internal sealed class WkStubWorkerRegistry : IWorkerRegistry
{
    private readonly IReadOnlyList<WorkerInfo> _workers;

    public WkStubWorkerRegistry(IEnumerable<WorkerInfo> workers)
    {
        _workers = workers.ToList();
    }

    public Task UpsertHeartbeatAsync(WorkerHeartbeat heartbeat, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<WorkerInfo>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_workers);
}
