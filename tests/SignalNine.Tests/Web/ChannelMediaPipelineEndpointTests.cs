using System.Net;
using System.Net.Http.Json;
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
public class ChannelMediaPipelineEndpointTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";
    private const string MediaPipelineJobType = "media.pipeline";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previousRootDirectory;
    private readonly StubJobs _stubJobs = new();

    public ChannelMediaPipelineEndpointTests()
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

    private static CreateChannelMediaRequest NewMovieRequest(string title = "Test Movie") =>
        new(
            Type: ChannelMediaType.Movies,
            Title: title,
            DurationSeconds: 7200,
            SourceType: MediaSourceType.Jellyfin,
            SourceRef: "jf-movie-pipeline-1",
            MovieReleaseYear: 2024,
            MovieDirector: "Director Name",
            TvSeriesName: null,
            TvSeason: null,
            TvEpisode: null,
            CommercialAdvertiser: null,
            CommercialCampaign: null,
            InformationEdition: null
        );

    [Fact]
    public async Task Post_Pipeline_Unauthenticated_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync($"/api/media/{Guid.NewGuid()}/pipeline", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_Pipeline_MissingMedia_Returns404()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var response = await client.PostAsync($"/api/media/{Guid.NewGuid()}/pipeline", content: null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_Pipeline_Success_ReturnsAcceptedAndEnqueues()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var createResp = await client.PostAsJsonAsync("/api/media", NewMovieRequest("Pipeline Movie"));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<ChannelMediaResponse>();
        Assert.NotNull(created);

        var expectedJobId = Guid.NewGuid();
        _stubJobs.SetNext(expectedJobId, MediaPipelineJobType);

        var response = await client.PostAsync($"/api/media/{created!.Id}/pipeline", content: null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JobResponse>();
        Assert.NotNull(body);
        Assert.Equal(expectedJobId, body!.Id);
        Assert.NotNull(_stubJobs.LastEnqueued);
        Assert.Equal(MediaPipelineJobType, _stubJobs.LastEnqueued!.Type);
        Assert.Contains(created.Id.ToString(), _stubJobs.LastEnqueued.PayloadJson);
    }

    [Fact]
    public async Task Post_Pipeline_InactiveMedia_Returns409()
    {
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var createResp = await client.PostAsJsonAsync("/api/media", NewMovieRequest("Inactive Movie"));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<ChannelMediaResponse>();
        Assert.NotNull(created);

        var update = new UpdateChannelMediaRequest(
            Type: created!.Type,
            Title: created.Title,
            DurationSeconds: created.DurationSeconds,
            IsActive: false,
            SourceType: created.SourceType,
            SourceRef: created.SourceRef,
            MovieReleaseYear: created.MovieReleaseYear,
            MovieDirector: created.MovieDirector,
            TvSeriesName: created.TvSeriesName,
            TvSeason: created.TvSeason,
            TvEpisode: created.TvEpisode,
            CommercialAdvertiser: created.CommercialAdvertiser,
            CommercialCampaign: created.CommercialCampaign,
            InformationEdition: created.InformationEdition
        );
        var putResp = await client.PutAsJsonAsync($"/api/media/{created.Id}", update);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var response = await client.PostAsync($"/api/media/{created.Id}/pipeline", content: null);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed class StubJobs : IJobManager
    {
        public EnqueueJobCommand? LastEnqueued { get; private set; }
        public Guid LastReturnedId { get; private set; } = Guid.NewGuid();
        private string _nextType = "media.pipeline";

        public void SetNext(Guid id, string type)
        {
            LastReturnedId = id;
            _nextType = type;
        }

        public Task<JobSnapshot> EnqueueAsync(EnqueueJobCommand command, CancellationToken cancellationToken = default)
        {
            LastEnqueued = command;
            var snapshot = new JobSnapshot
            {
                Id = LastReturnedId,
                Type = _nextType,
                State = JobStateType.Queued,
                Progress = new JobProgressSnapshot { Percent = 0, Message = "queued" },
                CreatedAt = DateTimeOffset.UtcNow
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
