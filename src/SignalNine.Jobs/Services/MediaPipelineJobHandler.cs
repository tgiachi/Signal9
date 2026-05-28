using System.Text.Json;
using SignalNine.Core.Data.Jobs;
using SignalNine.Core.Data.Jobs.Results;
using SignalNine.Core.Data.Pipeline;
using SignalNine.Core.Interfaces;
using SignalNine.Jobs.Services.Pipeline;

namespace SignalNine.Jobs.Services;

public class MediaPipelineJobHandler : IJobHandler
{
    public const string JobType = "media.pipeline";
    private const string OutputDirName = "output";

    private readonly ProbeMediaTask _probe;
    private readonly ExtractPreviewsTask _extract;

    public MediaPipelineJobHandler(ProbeMediaTask probe, ExtractPreviewsTask extract)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(extract);

        _probe = probe;
        _extract = extract;
    }

    public string Type => JobType;

    public async Task<IJobResult> ExecuteAsync(JobExecutionContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var payload = JsonSerializer.Deserialize<MediaPipelinePayloadV2>(context.PayloadJson)
                      ?? throw new InvalidOperationException("Empty pipeline payload.");

        var inputPath = Path.Combine(context.WorkDir, payload.InputFile);
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException(
                $"Pipeline input not found at '{inputPath}' for ChannelMedia {payload.ChannelMediaId}.",
                inputPath
            );
        }

        await context.ReportProgressAsync(10, "Probing media", cancellationToken).ConfigureAwait(false);
        var probeResult = await _probe.RunAsync(inputPath, cancellationToken).ConfigureAwait(false);

        await context.ReportProgressAsync(40, "Extracting previews", cancellationToken).ConfigureAwait(false);
        var outputDir = Path.Combine(context.WorkDir, OutputDirName);
        var previewFiles = await _extract.RunAsync(
            inputPath,
            outputDir,
            payload.PreviewCount,
            probeResult.DurationSeconds,
            cancellationToken
        ).ConfigureAwait(false);

        await context.ReportProgressAsync(100, "Done", cancellationToken).ConfigureAwait(false);

        return new MediaPipelineResult(
            payload.ChannelMediaId,
            probeResult.DurationSeconds,
            previewFiles,
            probeResult.Json
        );
    }
}
