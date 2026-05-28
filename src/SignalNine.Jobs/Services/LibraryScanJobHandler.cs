using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Channels;
using SignalNine.Core.Data.Jellyfin;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;
using ChannelMediaType = SignalNine.Persistence.Types.ChannelMediaType;

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

        var library = libraries.GetByKey(payload.MediaLibraryId)
                      ?? throw new InvalidOperationException($"MediaLibrary {payload.MediaLibraryId} not found.");

        if (!library.IsActive)
        {
            throw new InvalidOperationException($"MediaLibrary {library.Id} is inactive.");
        }

        var items = new List<ScannedItem>();
        var processed = 0;

        switch (library.SourceType)
        {
            case MediaSourceType.Jellyfin:
                var jellyfinItems = await jellyfin.ListItemsAsync(library.SourceRef, cancellationToken).ConfigureAwait(false);
                var total = jellyfinItems.Count;
                foreach (var item in jellyfinItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    items.Add(BuildScannedItemFromJellyfin(library, item));
                    processed++;
                    await MaybeReportProgressAsync(context, processed, total, cancellationToken).ConfigureAwait(false);
                }
                break;

            case MediaSourceType.LocalFile:
                var localItems = walker.Walk(library.SourceRef, cancellationToken).ToList();
                foreach (var item in localItems)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    items.Add(BuildScannedItemFromLocal(item));
                    processed++;
                    await MaybeReportProgressAsync(context, processed, localItems.Count, cancellationToken).ConfigureAwait(false);
                }
                break;

            case MediaSourceType.Url:
                throw new NotSupportedException("Url source scanning is not supported in v1.");

            default:
                throw new NotSupportedException($"Unknown source type {library.SourceType}.");
        }

        return new LibraryScanResult(library.Id, items);
    }

    private static ScannedItem BuildScannedItemFromJellyfin(MediaLibraryEntity library, JellyfinItem item)
    {
        int? movieReleaseYear = null;
        string? tvSeriesName = null;
        int? tvSeason = null;
        int? tvEpisode = null;

        switch (library.DefaultMediaType)
        {
            case ChannelMediaType.Movies:
                movieReleaseYear = item.ProductionYear;
                break;
            case ChannelMediaType.TvShow:
                tvSeriesName = item.SeriesName;
                tvSeason = item.ParentIndexNumber;
                tvEpisode = item.IndexNumber;
                break;
        }

        return new ScannedItem(
            Title: item.Name,
            SourceRef: item.Id,
            SourceType: (int)MediaSourceType.Jellyfin,
            DurationSeconds: TicksToSeconds(item.RunTimeTicks),
            MovieReleaseYear: movieReleaseYear,
            MovieDirector: null,
            TvSeriesName: tvSeriesName,
            TvSeason: tvSeason,
            TvEpisode: tvEpisode
        );
    }

    private static ScannedItem BuildScannedItemFromLocal(LocalLibraryItem item)
    {
        return new ScannedItem(
            Title: item.Title,
            SourceRef: item.RelativePath,
            SourceType: (int)MediaSourceType.LocalFile,
            DurationSeconds: null,
            MovieReleaseYear: null,
            MovieDirector: null,
            TvSeriesName: null,
            TvSeason: null,
            TvEpisode: null
        );
    }

    private static int? TicksToSeconds(long? ticks)
    {
        return ticks is null ? null : (int)(ticks.Value / 10_000_000);
    }

    private static async Task MaybeReportProgressAsync(
        JobExecutionContext context,
        int processed,
        int total,
        CancellationToken ct
    )
    {
        if (processed % ProgressReportInterval != 0 && processed != total) return;

        var percent = total == 0 ? 100 : Math.Clamp(processed * 100 / total, 0, 100);
        await context.ReportProgressAsync(percent, $"Processed {processed}/{total}", ct).ConfigureAwait(false);
    }
}
