using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Channels;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Data.Pipeline;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;

namespace SignalNine.Jobs.Services;

public class LibraryScanJobHandler : IJobHandler
{
    public const string JobType = "library.scan";
    private const int ProgressReportInterval = 10;

    private readonly IServiceScopeFactory _scopeFactory;

    public LibraryScanJobHandler(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public string Type => JobType;

    public async Task<IJobResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var payload = JsonSerializer.Deserialize<ScanLibraryPayload>(context.PayloadJson)
                      ?? throw new InvalidOperationException("Empty scan payload.");

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var jellyfin = sp.GetRequiredService<IJellyfinService>();
        var walker = sp.GetRequiredService<ILocalLibraryWalker>();
        var libraries = sp.GetRequiredService<IDataAccess<MediaLibraryEntity>>();
        var media = sp.GetRequiredService<IDataAccess<ChannelMediaEntity>>();
        var jobs = sp.GetRequiredService<IJobManager>();

        var library = libraries.GetByKey(payload.MediaLibraryId)
                      ?? throw new InvalidOperationException($"MediaLibrary {payload.MediaLibraryId} not found.");

        if (!library.IsActive)
        {
            throw new InvalidOperationException($"MediaLibrary {library.Id} is inactive.");
        }

        var processed = 0;

        switch (library.SourceType)
        {
            case MediaSourceType.Jellyfin:
                var items = await jellyfin.ListItemsAsync(library.SourceRef, cancellationToken).ConfigureAwait(false);
                var total = items.Count;
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await TryUpsertJellyfinItemAsync(media, jobs, library, item, context).ConfigureAwait(false);
                    processed++;
                    await MaybeReportProgressAsync(jobs, context, processed, total, cancellationToken).ConfigureAwait(false);
                }
                break;

            case MediaSourceType.LocalFile:
                var localItems = walker.Walk(library.SourceRef, cancellationToken).ToList();
                foreach (var item in localItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await TryUpsertLocalItemAsync(media, jobs, library, item, context).ConfigureAwait(false);
                    processed++;
                    await MaybeReportProgressAsync(jobs, context, processed, localItems.Count, cancellationToken).ConfigureAwait(false);
                }
                break;

            case MediaSourceType.Url:
                throw new NotSupportedException("Url source scanning is not supported in v1.");

            default:
                throw new NotSupportedException($"Unknown source type {library.SourceType}.");
        }

        library.LastScannedAt = DateTime.UtcNow;
        library.UpdatedAt = DateTime.UtcNow;
        libraries.Update(library);

        // Stub result — will be replaced with typed LibraryScanResult in T8-T11
        return new EmptyJobResult(JobType);
    }

    // Kept as a literal to avoid coupling this handler to MediaPipelineJobHandler.
    private const string PipelineJobType = "media.pipeline";

    private static async Task TryUpsertJellyfinItemAsync(
        IDataAccess<ChannelMediaEntity> media,
        IJobManager jobs,
        MediaLibraryEntity library,
        JellyfinItem item,
        JobExecutionContext context
    )
    {
        try
        {
            var existing = FindExisting(media, library.Id, MediaSourceType.Jellyfin, item.Id);
            if (existing is null)
            {
                var inserted = media.Insert(BuildChannelMediaFromJellyfin(library, item));
                await EnqueuePipelineForNewAsync(jobs, inserted.Id, context).ConfigureAwait(false);
            }
            else
            {
                MutateFromJellyfin(existing, library, item);
                media.Update(existing);
            }
        }
        catch (Exception ex)
        {
            await jobs.WriteLogAsync(context.JobId, JobLogLevelType.Warning,
                $"Failed to upsert Jellyfin item {item.Id}: {ex.Message}", CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task TryUpsertLocalItemAsync(
        IDataAccess<ChannelMediaEntity> media,
        IJobManager jobs,
        MediaLibraryEntity library,
        LocalLibraryItem item,
        JobExecutionContext context
    )
    {
        try
        {
            var existing = FindExisting(media, library.Id, MediaSourceType.LocalFile, item.RelativePath);
            if (existing is null)
            {
                var inserted = media.Insert(BuildChannelMediaFromLocal(library, item));
                await EnqueuePipelineForNewAsync(jobs, inserted.Id, context).ConfigureAwait(false);
            }
            else
            {
                MutateFromLocal(existing, library, item);
                media.Update(existing);
            }
        }
        catch (Exception ex)
        {
            await jobs.WriteLogAsync(context.JobId, JobLogLevelType.Warning,
                $"Failed to upsert local file {item.RelativePath}: {ex.Message}", CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static async Task EnqueuePipelineForNewAsync(
        IJobManager jobs,
        Guid channelMediaId,
        JobExecutionContext context
    )
    {
        try
        {
            var payload = JsonSerializer.Serialize(new MediaPipelinePayload(channelMediaId));
            var snap = await jobs.EnqueueAsync(
                new EnqueueJobCommand { Type = PipelineJobType, PayloadJson = payload },
                CancellationToken.None
            ).ConfigureAwait(false);

            await jobs.WriteLogAsync(context.JobId, JobLogLevelType.Information,
                $"Enqueued media.pipeline job {snap.Id} for ChannelMedia {channelMediaId}",
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await jobs.WriteLogAsync(context.JobId, JobLogLevelType.Warning,
                $"Failed to enqueue media.pipeline for {channelMediaId}: {ex.Message}",
                CancellationToken.None).ConfigureAwait(false);
        }
    }

    private static ChannelMediaEntity? FindExisting(
        IDataAccess<ChannelMediaEntity> media,
        Guid mediaLibraryId,
        MediaSourceType sourceType,
        string sourceRef
    )
    {
        return media.List().FirstOrDefault(m =>
            m.MediaLibraryId == mediaLibraryId &&
            m.SourceType == sourceType &&
            m.SourceRef == sourceRef);
    }

    private static ChannelMediaEntity BuildChannelMediaFromJellyfin(MediaLibraryEntity library, JellyfinItem item)
    {
        var entity = new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = library.DefaultMediaType,
            Title = item.Name,
            DurationSeconds = TicksToSeconds(item.RunTimeTicks),
            IsActive = true,
            SourceType = MediaSourceType.Jellyfin,
            SourceRef = item.Id,
            MediaLibraryId = library.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ApplyTypeSpecificFromJellyfin(entity, library, item);
        return entity;
    }

    private static void MutateFromJellyfin(ChannelMediaEntity existing, MediaLibraryEntity library, JellyfinItem item)
    {
        existing.Title = item.Name;
        existing.DurationSeconds = TicksToSeconds(item.RunTimeTicks);
        existing.Type = library.DefaultMediaType;
        existing.UpdatedAt = DateTime.UtcNow;
        ApplyTypeSpecificFromJellyfin(existing, library, item);
    }

    private static void ApplyTypeSpecificFromJellyfin(ChannelMediaEntity entity, MediaLibraryEntity library, JellyfinItem item)
    {
        switch (library.DefaultMediaType)
        {
            case ChannelMediaType.Movies:
                entity.MovieReleaseYear = item.ProductionYear;
                break;
            case ChannelMediaType.TvShow:
                entity.TvSeriesName = item.SeriesName;
                entity.TvSeason = item.ParentIndexNumber;
                entity.TvEpisode = item.IndexNumber;
                break;
        }
    }

    private static ChannelMediaEntity BuildChannelMediaFromLocal(MediaLibraryEntity library, LocalLibraryItem item)
    {
        return new ChannelMediaEntity
        {
            Id = Guid.NewGuid(),
            Type = library.DefaultMediaType,
            Title = item.Title,
            DurationSeconds = null,
            IsActive = true,
            SourceType = MediaSourceType.LocalFile,
            SourceRef = item.RelativePath,
            MediaLibraryId = library.Id,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static void MutateFromLocal(ChannelMediaEntity existing, MediaLibraryEntity library, LocalLibraryItem item)
    {
        existing.Title = item.Title;
        existing.Type = library.DefaultMediaType;
        existing.UpdatedAt = DateTime.UtcNow;
    }

    private static int? TicksToSeconds(long? ticks)
    {
        return ticks is null ? null : (int)(ticks.Value / 10_000_000);
    }

    private static async Task MaybeReportProgressAsync(
        IJobManager jobs,
        JobExecutionContext context,
        int processed,
        int total,
        CancellationToken ct
    )
    {
        if (processed % ProgressReportInterval != 0 && processed != total) return;

        var percent = total == 0 ? 100 : Math.Clamp(processed * 100 / total, 0, 100);
        await jobs.ReportProgressAsync(context.JobId, percent, $"Processed {processed}/{total}", ct).ConfigureAwait(false);
    }
}
