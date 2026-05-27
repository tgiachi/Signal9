using System.Text.Json;
using SignalNine.Core.Data.Channels;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;

namespace SignalNine.Web.Services;

public class LibraryScanJobHandler : IJobHandler
{
    public const string JobType = "library.scan";
    private const int ProgressReportInterval = 10;

    private readonly IJellyfinService _jellyfin;
    private readonly ILocalLibraryWalker _walker;
    private readonly IDataAccess<MediaLibraryEntity> _libraries;
    private readonly IDataAccess<ChannelMediaEntity> _media;
    private readonly IJobManager _jobs;

    public LibraryScanJobHandler(
        IJellyfinService jellyfin,
        ILocalLibraryWalker walker,
        IDataAccess<MediaLibraryEntity> libraries,
        IDataAccess<ChannelMediaEntity> media,
        IJobManager jobs
    )
    {
        ArgumentNullException.ThrowIfNull(jellyfin);
        ArgumentNullException.ThrowIfNull(walker);
        ArgumentNullException.ThrowIfNull(libraries);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(jobs);

        _jellyfin = jellyfin;
        _walker = walker;
        _libraries = libraries;
        _media = media;
        _jobs = jobs;
    }

    public string Type => JobType;

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var payload = JsonSerializer.Deserialize<ScanLibraryPayload>(context.PayloadJson)
                      ?? throw new InvalidOperationException("Empty scan payload.");

        var library = _libraries.GetByKey(payload.MediaLibraryId)
                      ?? throw new InvalidOperationException($"MediaLibrary {payload.MediaLibraryId} not found.");

        if (!library.IsActive)
        {
            throw new InvalidOperationException($"MediaLibrary {library.Id} is inactive.");
        }

        var processed = 0;

        switch (library.SourceType)
        {
            case MediaSourceType.Jellyfin:
                var items = await _jellyfin.ListItemsAsync(library.SourceRef, cancellationToken).ConfigureAwait(false);
                var total = items.Count;
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TryUpsertJellyfinItem(library, item, context);
                    processed++;
                    await MaybeReportProgressAsync(context, processed, total, cancellationToken).ConfigureAwait(false);
                }
                break;

            case MediaSourceType.LocalFile:
                var localItems = _walker.Walk(library.SourceRef, cancellationToken).ToList();
                foreach (var item in localItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    TryUpsertLocalItem(library, item, context);
                    processed++;
                    await MaybeReportProgressAsync(context, processed, localItems.Count, cancellationToken).ConfigureAwait(false);
                }
                break;

            case MediaSourceType.Url:
                throw new NotSupportedException("Url source scanning is not supported in v1.");

            default:
                throw new NotSupportedException($"Unknown source type {library.SourceType}.");
        }

        library.LastScannedAt = DateTime.UtcNow;
        library.UpdatedAt = DateTime.UtcNow;
        _libraries.Update(library);
    }

    private void TryUpsertJellyfinItem(MediaLibraryEntity library, JellyfinItem item, JobExecutionContext context)
    {
        try
        {
            var existing = FindExisting(library.Id, MediaSourceType.Jellyfin, item.Id);
            if (existing is null)
            {
                _media.Insert(BuildChannelMediaFromJellyfin(library, item));
            }
            else
            {
                MutateFromJellyfin(existing, library, item);
                _media.Update(existing);
            }
        }
        catch (Exception ex)
        {
            _ = _jobs.WriteLogAsync(context.JobId, JobLogLevelType.Warning,
                $"Failed to upsert Jellyfin item {item.Id}: {ex.Message}", CancellationToken.None);
        }
    }

    private void TryUpsertLocalItem(MediaLibraryEntity library, LocalLibraryItem item, JobExecutionContext context)
    {
        try
        {
            var existing = FindExisting(library.Id, MediaSourceType.LocalFile, item.RelativePath);
            if (existing is null)
            {
                _media.Insert(BuildChannelMediaFromLocal(library, item));
            }
            else
            {
                MutateFromLocal(existing, library, item);
                _media.Update(existing);
            }
        }
        catch (Exception ex)
        {
            _ = _jobs.WriteLogAsync(context.JobId, JobLogLevelType.Warning,
                $"Failed to upsert local file {item.RelativePath}: {ex.Message}", CancellationToken.None);
        }
    }

    private ChannelMediaEntity? FindExisting(Guid mediaLibraryId, MediaSourceType sourceType, string sourceRef)
    {
        return _media.List().FirstOrDefault(m =>
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

    private async Task MaybeReportProgressAsync(JobExecutionContext context, int processed, int total, CancellationToken ct)
    {
        if (processed % ProgressReportInterval != 0 && processed != total) return;

        var percent = total == 0 ? 100 : Math.Clamp(processed * 100 / total, 0, 100);
        await _jobs.ReportProgressAsync(context.JobId, percent, $"Processed {processed}/{total}", ct).ConfigureAwait(false);
    }
}
