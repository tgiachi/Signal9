using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;
using SignalNine.Persistence.Types;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Channels;
using SignalNine.Web.Data.Jobs;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class MediaLibraryScanEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";
    private const string LibraryScanJobType = "library.scan";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;
    private readonly StubJobManager _stubJobs = new();

    public MediaLibraryScanEndpointTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"signalnine-tests-{Guid.NewGuid():N}");
        _previousRootDirectory = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);

        _factory = new WebApplicationFactory<Program>()
                   .WithWebHostBuilder(builder =>
                   {
                       builder.ConfigureServices(services =>
                       {
                           services.AddSingleton<IJobManager>(_stubJobs);
                       });
                   });
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

    private static CreateMediaLibraryRequest NewLib(string sourceRef = "jf-lib-scan")
    {
        return new(
            Name: "Scan target",
            Description: null,
            DefaultMediaType: ChannelMediaType.Movies,
            SourceType: MediaSourceType.Jellyfin,
            SourceRef: sourceRef
        );
    }

    [Fact]
    public async Task Post_Scan_Unauthenticated_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/media-libraries/{Guid.NewGuid()}/scan", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Scan_LibraryMissing_Returns404()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsync($"/api/media-libraries/{Guid.NewGuid()}/scan", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Scan_LibraryExists_ReturnsAcceptedAndEnqueues()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var createResp = await client.PostAsJsonAsync("/api/media-libraries", NewLib("scan-1"));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<MediaLibraryResponse>();
        Assert.NotNull(created);

        _stubJobs.SetNextSnapshot(Guid.NewGuid(), LibraryScanJobType);

        var response = await client.PostAsync($"/api/media-libraries/{created!.Id}/scan", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(body);
        Assert.Equal(_stubJobs.LastReturnedSnapshotId, body!.Id);
        Assert.NotNull(_stubJobs.LastEnqueued);
        Assert.Equal(LibraryScanJobType, _stubJobs.LastEnqueued!.Type);
        Assert.Contains(created.Id.ToString(), _stubJobs.LastEnqueued.PayloadJson);
    }

    [Fact]
    public async Task Post_Scan_InactiveLibrary_Returns409()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var createResp = await client.PostAsJsonAsync("/api/media-libraries", NewLib("scan-2"));
        var created = await createResp.Content.ReadFromJsonAsync<MediaLibraryResponse>();
        Assert.NotNull(created);

        var update = new UpdateMediaLibraryRequest(
            Name: created!.Name,
            Description: created.Description,
            DefaultMediaType: created.DefaultMediaType,
            IsActive: false,
            SourceType: created.SourceType,
            SourceRef: created.SourceRef
        );
        var putResp = await client.PutAsJsonAsync($"/api/media-libraries/{created.Id}", update);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var response = await client.PostAsync($"/api/media-libraries/{created.Id}/scan", content: null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed class StubJobManager : IJobManager
    {
        public EnqueueJobCommand? LastEnqueued { get; private set; }
        public Guid LastReturnedSnapshotId { get; private set; } = Guid.NewGuid();
        private string _nextType = "library.scan";

        public void SetNextSnapshot(Guid id, string type)
        {
            LastReturnedSnapshotId = id;
            _nextType = type;
        }

        public Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default)
        {
            LastEnqueued = command;
            var snapshot = new JobSnapshot
            {
                Id = LastReturnedSnapshotId,
                Type = _nextType,
                State = JobStateType.Queued,
                Progress = new JobProgressSnapshot { Percent = 0, Message = "queued" },
                CreatedAt = DateTime.UtcNow
            };
            return Task.FromResult(snapshot);
        }
        public IReadOnlyList<JobSnapshot> List()
        {
            return Array.Empty<JobSnapshot>();
        }
        public JobSnapshot? GetById(Guid jobId)
        {
            return null;
        }
        public IReadOnlyList<JobLogEntry> GetLogs(Guid jobId)
        {
            return Array.Empty<JobLogEntry>();
        }
        public Task<bool> CancelAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
        public async ValueTask<Guid> DequeueAsync(CancellationToken cancellationToken)
        {
            // Block forever to keep the BackgroundService idle during tests.
            await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
            return Guid.Empty;
        }
        public Task<JobExecutionContext?> StartAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
        public Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public Task FailAsync(Guid jobId, Exception exception, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public Task MarkCanceledAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public Task ReportProgressAsync(Guid jobId, int percent, string message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public Task WriteLogAsync(Guid jobId, JobLogLevelType level, string message, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
        public CancellationToken GetCancellationToken(Guid jobId)
        {
            return CancellationToken.None;
        }
    }
}
