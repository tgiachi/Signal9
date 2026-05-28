using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;
using SignalNine.Persistence.Types;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Channels;
using SignalNine.Web.Data.Jobs;
using SignalNine.Web.Services;

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
                           // Override WorkSpaceStager so staging uses the temp root directory.
                           var workspaceConfig = new SignalNineConfig
                           {
                               WorkSpace = new WorkSpaceConfig { Path = _rootDirectory }
                           };
                           services.AddSingleton(new WorkSpaceStager(workspaceConfig));
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
    public async Task Post_ProcessAll_LibraryMissing_Returns404()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsync($"/api/media-libraries/{Guid.NewGuid()}/process-all", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_ProcessAll_EnqueuesPipelinePerMedia()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        // Use a LocalFile library whose SourceRef is a real directory on disk.
        var libSourceDir = Path.Combine(Path.GetTempPath(), $"signalnine-lib-{Guid.NewGuid():N}");
        Directory.CreateDirectory(libSourceDir);
        try
        {
            var createResp = await client.PostAsJsonAsync("/api/media-libraries", new CreateMediaLibraryRequest(
                Name: "LocalFile library",
                Description: null,
                DefaultMediaType: ChannelMediaType.Movies,
                SourceType: MediaSourceType.LocalFile,
                SourceRef: libSourceDir
            ));
            Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
            var lib = await createResp.Content.ReadFromJsonAsync<MediaLibraryResponse>();
            Assert.NotNull(lib);

            // Pre-create dummy source files for staging.
            var fileRefs = new string[3];
            for (var i = 0; i < 3; i++)
            {
                var filename = $"movie-{i}.mp4";
                fileRefs[i] = filename;
                File.WriteAllText(Path.Combine(libSourceDir, filename), "dummy");
            }

            using (var scope = _factory.Services.CreateScope())
            {
                var media = scope.ServiceProvider
                    .GetRequiredService<SignalNine.Persistence.Interfaces.IDataAccess<SignalNine.Persistence.Entities.Channels.ChannelMediaEntity>>();
                for (var i = 0; i < 3; i++)
                {
                    media.Insert(new SignalNine.Persistence.Entities.Channels.ChannelMediaEntity
                    {
                        Id = Guid.NewGuid(),
                        Type = ChannelMediaType.Movies,
                        Title = $"Movie {i}",
                        IsActive = true,
                        SourceType = MediaSourceType.LocalFile,
                        SourceRef = fileRefs[i],
                        MediaLibraryId = lib!.Id,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
            }

            _stubJobs.ClearAllEnqueued();

            var response = await client.PostAsync($"/api/media-libraries/{lib!.Id}/process-all", content: null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<ProcessAllMediaLibraryResponse>();
            Assert.NotNull(body);
            Assert.Equal(3, body!.EnqueuedCount);
            Assert.Equal(3, _stubJobs.AllEnqueued.Count);
            Assert.All(_stubJobs.AllEnqueued, c => Assert.Equal("media.pipeline", c.Type));
        }
        finally
        {
            if (Directory.Exists(libSourceDir))
            {
                try { Directory.Delete(libSourceDir, recursive: true); } catch { /* best effort */ }
            }
        }
    }

    [Fact]
    public async Task Post_ProcessAll_InactiveLibrary_Returns409()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var createResp = await client.PostAsJsonAsync("/api/media-libraries", NewLib("pa-inactive"));
        var lib = await createResp.Content.ReadFromJsonAsync<MediaLibraryResponse>();
        Assert.NotNull(lib);

        var update = new UpdateMediaLibraryRequest(
            Name: lib!.Name,
            Description: lib.Description,
            DefaultMediaType: lib.DefaultMediaType,
            IsActive: false,
            SourceType: lib.SourceType,
            SourceRef: lib.SourceRef
        );
        await client.PutAsJsonAsync($"/api/media-libraries/{lib.Id}", update);

        var response = await client.PostAsync($"/api/media-libraries/{lib.Id}/process-all", content: null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
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
        public List<EnqueueJobCommand> AllEnqueued { get; } = new();
        public Guid LastReturnedSnapshotId { get; private set; } = Guid.NewGuid();
        private bool _nextIdConsumed = true;

        public void SetNextSnapshot(Guid id, string type)
        {
            LastReturnedSnapshotId = id;
            _nextIdConsumed = false;
        }

        public void ClearAllEnqueued()
        {
            AllEnqueued.Clear();
            LastEnqueued = null;
        }

        public Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default)
        {
            LastEnqueued = command;
            AllEnqueued.Add(command);
            var id = _nextIdConsumed ? Guid.NewGuid() : LastReturnedSnapshotId;
            _nextIdConsumed = true;
            var snapshot = new JobSnapshot
            {
                Id = id,
                Type = command.Type,
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

        public ValueTask<Guid> DequeueAsync(JobStreamTarget target, CancellationToken cancellationToken)
            => DequeueAsync(cancellationToken);

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
