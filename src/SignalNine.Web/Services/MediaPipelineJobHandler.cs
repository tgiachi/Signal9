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

        foreach (var task in tasks.OrderBy(t => t.Order))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!task.IsEnabled)
            {
                continue;
            }

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
    }
}
