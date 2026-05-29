using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using SignalNine.Tests.Support.Web;
using SignalNine.Web.Data.Schedule;

namespace SignalNine.Tests.Web;

[Collection(WebApplicationCollection.Name)]
public class ScheduleEndpointsTests : IDisposable
{
    private const string RootDirectoryVariableName = "SIGNAL9_ROOT_DIRECTORY";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _rootDirectory;
    private readonly string? _previous;

    public ScheduleEndpointsTests()
    {
        _rootDirectory = Path.Combine(Path.GetTempPath(), $"schedule-ep-{Guid.NewGuid():N}");
        _previous = Environment.GetEnvironmentVariable(RootDirectoryVariableName);
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _rootDirectory);
        _factory = new WebApplicationFactory<Program>();
    }

    public void Dispose()
    {
        _factory.Dispose();
        Environment.SetEnvironmentVariable(RootDirectoryVariableName, _previous);
        if (Directory.Exists(_rootDirectory)) Directory.Delete(_rootDirectory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task BlocksCrud_RoundTrip()
    {
        var channelId = SeedChannel();
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var create = new ScheduleBlockRequest(
            Name: "Mon prime time",
            DayOfWeek: DayOfWeek.Monday,
            StartTime: new TimeSpan(20, 0, 0),
            DurationMinutes: 120,
            RuleType: ScheduleBlockRuleType.TagPool,
            PinnedChannelMediaId: null,
            SeriesName: null,
            TagFilterCsv: "action",
            TypeFilterCsv: "Movies",
            IsActive: true);

        var createResp = await client.PostAsJsonAsync($"/api/channels/{channelId}/schedule/blocks", create);
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);
        var created = await createResp.Content.ReadFromJsonAsync<ScheduleBlockResponse>();
        Assert.NotNull(created);

        var listResp = await client.GetAsync($"/api/channels/{channelId}/schedule/blocks");
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var list = await listResp.Content.ReadFromJsonAsync<List<ScheduleBlockResponse>>();
        Assert.Contains(list!, b => b.Id == created!.Id);

        var update = create with { Name = "renamed" };
        var updateResp = await client.PutAsJsonAsync($"/api/schedule/blocks/{created!.Id}", update);
        Assert.Equal(HttpStatusCode.OK, updateResp.StatusCode);
        var updated = await updateResp.Content.ReadFromJsonAsync<ScheduleBlockResponse>();
        Assert.Equal("renamed", updated!.Name);

        var delResp = await client.DeleteAsync($"/api/schedule/blocks/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);
    }

    [Fact]
    public async Task Timeline_ReturnsEntries()
    {
        var channelId = SeedChannel();
        var (mediaId, _) = SeedMovie(durationSeconds: 600);
        SeedEntry(channelId, mediaId, DateTime.UtcNow.AddMinutes(-5), durationSeconds: 600);

        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var resp = await client.GetAsync($"/api/channels/{channelId}/schedule/timeline");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ScheduleTimelineResponse>();
        Assert.NotNull(body);
        Assert.NotEmpty(body!.Entries);
    }

    [Fact]
    public async Task Now_ReturnsCurrentAndNext()
    {
        var channelId = SeedChannel();
        var (mediaId, _) = SeedMovie(durationSeconds: 600);
        var first = SeedEntry(channelId, mediaId, DateTime.UtcNow.AddMinutes(-1), durationSeconds: 600);
        SeedEntry(channelId, mediaId, first.StartAt.AddSeconds(600), durationSeconds: 600);

        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());
        var resp = await client.GetAsync($"/api/channels/{channelId}/schedule/now");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ScheduleNowResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.Current);
        Assert.NotNull(body.Next);
        Assert.True(body.SecondsIntoCurrent >= 0);
    }

    [Fact]
    public async Task Rebuild_EnqueuesJobAndReturnsAccepted()
    {
        var channelId = SeedChannel();
        using var client = JwtClientFactory.CreateAuthorizedClient(_factory.CreateClient());

        var resp = await client.PostAsJsonAsync(
            $"/api/channels/{channelId}/schedule/rebuild",
            new ScheduleRebuildRequest(FromUtc: null, HoursAhead: 24));

        Assert.Equal(HttpStatusCode.Accepted, resp.StatusCode);
    }

    private Guid SeedChannel()
    {
        using var scope = _factory.Services.CreateScope();
        var channels = scope.ServiceProvider.GetRequiredService<IDataAccess<ChannelEntity>>();
        var channel = new ChannelEntity
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Slug = $"test-{Guid.NewGuid():N}",
            IsActive = true
        };
        channels.Insert(channel);
        return channel.Id;
    }

    private (Guid Id, ChannelMediaEntity Entity) SeedMovie(int durationSeconds)
    {
        using var scope = _factory.Services.CreateScope();
        var media = scope.ServiceProvider.GetRequiredService<IDataAccess<ChannelMediaEntity>>();
        var entity = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Title = "Test Movie",
            Type = ChannelMediaType.Movies,
            DurationSeconds = durationSeconds,
            IsActive = true
        };
        media.Insert(entity);
        return (entity.Id, entity);
    }

    private ScheduledEntryEntity SeedEntry(Guid channelId, Guid mediaId, DateTime startAt, int durationSeconds)
    {
        using var scope = _factory.Services.CreateScope();
        var entries = scope.ServiceProvider.GetRequiredService<IDataAccess<ScheduledEntryEntity>>();
        var entry = new ScheduledEntryEntity
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            StartAt = startAt,
            DurationSeconds = durationSeconds,
            Kind = ScheduledEntryKind.Media,
            ChannelMediaId = mediaId,
            MediaPartIndex = 0,
            MediaPartCount = 1,
            MediaOffsetSeconds = 0
        };
        entries.Insert(entry);
        return entry;
    }
}
