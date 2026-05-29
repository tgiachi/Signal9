using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Pipeline;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Types;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Data.Pipeline;
using SignalNine.Web.Interfaces;

namespace SignalNine.Web.Services;

public class MediaPipelineJobHandler : IJobHandler
{
    public const string JobType = "media.pipeline";

    private readonly IServiceScopeFactory _scopeFactory;

    public MediaPipelineJobHandler(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public string Type => JobType;

    public async Task ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var payload = JsonSerializer.Deserialize<MediaPipelinePayload>(context.PayloadJson)
                      ?? throw new InvalidOperationException("Empty pipeline payload.");

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var media = sp.GetRequiredService<IDataAccess<ChannelMediaEntity>>();
        var libraries = sp.GetRequiredService<IDataAccess<MediaLibraryEntity>>();
        var resolver = sp.GetRequiredService<IMediaPathResolver>();
        var jobs = sp.GetRequiredService<IJobManager>();
        var tasks = sp.GetServices<IPipelineTask>();

        var mediaEntity = media.GetByKey(payload.ChannelMediaId)
                          ?? throw new InvalidOperationException(
                              $"ChannelMedia {payload.ChannelMediaId} not found.");

        if (mediaEntity.MediaLibraryId is null)
        {
            throw new InvalidOperationException(
                $"ChannelMedia {mediaEntity.Id} has no MediaLibraryId.");
        }

        var library = libraries.GetByKey(mediaEntity.MediaLibraryId.Value)
                      ?? throw new InvalidOperationException(
                          $"MediaLibrary {mediaEntity.MediaLibraryId} not found.");

        var resolvedPath = await resolver.ResolveAsync(mediaEntity, library, cancellationToken).ConfigureAwait(false);
        var pipelineContext = new PipelineContext(mediaEntity, library, resolvedPath, context);

        var displayName = BuildDisplayName(mediaEntity);

        var enabledTasks = tasks
            .Where(t => t.IsEnabled)
            .OrderBy(t => t.Order)
            .ToList();

        if (enabledTasks.Count == 0)
        {
            await jobs.ReportProgressAsync(
                context.JobId,
                100,
                $"No pipeline tasks enabled · {displayName}",
                cancellationToken
            ).ConfigureAwait(false);
        }

        for (var i = 0; i < enabledTasks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var task = enabledTasks[i];
            var percent = (int)((double)i / enabledTasks.Count * 100);

            await jobs.ReportProgressAsync(
                context.JobId,
                percent,
                $"{task.Name} · {displayName}",
                cancellationToken
            ).ConfigureAwait(false);

            try
            {
                await task.ExecuteAsync(pipelineContext, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                await jobs.WriteLogAsync(
                    context.JobId,
                    JobLogLevelType.Warning,
                    $"Pipeline task '{task.Name}' failed: {ex.Message}",
                    CancellationToken.None
                ).ConfigureAwait(false);
            }
        }

        mediaEntity.UpdatedAt = DateTime.UtcNow;
        media.Update(mediaEntity);

        if (enabledTasks.Count > 0)
        {
            await jobs.ReportProgressAsync(
                context.JobId,
                100,
                $"Done · {displayName}",
                cancellationToken
            ).ConfigureAwait(false);
        }
    }

    private static string BuildDisplayName(ChannelMediaEntity media)
    {
        var title = string.IsNullOrWhiteSpace(media.Title)
            ? media.Id.ToString()[..8]
            : media.Title;

        if (!string.IsNullOrWhiteSpace(media.TvSeriesName))
        {
            var season = media.TvSeason ?? 0;
            var episode = media.TvEpisode ?? 0;
            return $"{media.TvSeriesName} S{season:00}E{episode:00} - {title}";
        }

        return title;
    }
}
