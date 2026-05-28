using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Data.Pipeline;
using SignalNine.Core.Interfaces;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;

namespace SignalNine.Web.Services.Results;

public sealed class LibraryScanResultProcessor : IJobResultProcessor
{
    public const string TargetType = "library.scan";
    private const string PipelineJobType = "media.pipeline";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WorkSpaceStager _stager;

    public LibraryScanResultProcessor(IServiceScopeFactory scopeFactory, WorkSpaceStager stager)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(stager);

        _scopeFactory = scopeFactory;
        _stager = stager;
    }

    public string JobType => TargetType;

    public async Task ApplyAsync(Guid jobId, string? resultJson, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(resultJson)) return;

        var result = JsonSerializer.Deserialize<LibraryScanResult>(resultJson)
                     ?? throw new InvalidOperationException("Empty library scan result.");

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var media = sp.GetRequiredService<IDataAccess<ChannelMediaEntity>>();
        var libraries = sp.GetRequiredService<IDataAccess<MediaLibraryEntity>>();
        var jobs = sp.GetRequiredService<IJobManager>();

        var library = libraries.GetByKey(result.LibraryId)
                      ?? throw new InvalidOperationException($"Library {result.LibraryId} not found.");

        foreach (var item in result.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceTypeEnum = (MediaSourceType)item.SourceType;
            var existing = media.List().FirstOrDefault(m =>
                m.MediaLibraryId == library.Id &&
                m.SourceType == sourceTypeEnum &&
                m.SourceRef == item.SourceRef);
            if (existing is not null) continue;

            var entity = new ChannelMediaEntity
            {
                Id = Guid.NewGuid(),
                Title = item.Title,
                SourceType = sourceTypeEnum,
                SourceRef = item.SourceRef,
                MediaLibraryId = library.Id,
                Type = library.DefaultMediaType,
                IsActive = true,
                DurationSeconds = item.DurationSeconds,
                MovieReleaseYear = item.MovieReleaseYear,
                MovieDirector = item.MovieDirector,
                TvSeriesName = item.TvSeriesName,
                TvSeason = item.TvSeason,
                TvEpisode = item.TvEpisode,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            media.Insert(entity);

            var (_, inputRel) = await _stager.StageAsync(entity, library, cancellationToken)
                                             .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(inputRel)) continue;

            var payload = JsonSerializer.Serialize(new MediaPipelinePayloadV2(entity.Id, inputRel));
            await jobs.EnqueueAsync(
                new EnqueueJobCommand { Type = PipelineJobType, PayloadJson = payload },
                cancellationToken
            ).ConfigureAwait(false);
        }

        library.LastScannedAt = DateTime.UtcNow;
        library.UpdatedAt = DateTime.UtcNow;
        libraries.Update(library);
    }
}
