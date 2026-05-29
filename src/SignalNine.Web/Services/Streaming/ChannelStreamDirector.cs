using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Data.Streaming;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services.Streaming;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Web.Data.Streaming;
using SignalNine.Web.Interfaces;
using ILogger = Serilog.ILogger;

namespace SignalNine.Web.Services.Streaming;

public sealed class ChannelStreamDirector
{
    private const int HlsListSize = 6;
    private const int HlsTimeSeconds = 4;
    private const int DefaultFps = 25;
    private const string FfmpegExecutable = "ffmpeg";
    private static readonly TimeSpan IdleStopAfter = TimeSpan.FromSeconds(60);

    private readonly ILogger _logger = Log.ForContext<ChannelStreamDirector>();
    private readonly Guid _channelId;
    private readonly string _outputDir;
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly object _lock = new();
    private int _nextSegmentNumber;
    private double _nextTsOffsetSeconds;
    private DateTime _lastTouchedUtc = DateTime.UtcNow;
    private CancellationTokenSource? _cts;
    private Task? _loop;
    private Guid? _currentEntryId;
    private string? _currentEntryTitle;
    private string? _currentEntryKind;
    private int? _currentEntryPartIndex;
    private int? _currentEntryPartCount;
    private int? _ffmpegPid;

    public ChannelStreamDirector(Guid channelId, string outputDir, IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _channelId = channelId;
        _outputDir = outputDir;
        _scopeFactory = scopeFactory;
        Directory.CreateDirectory(_outputDir);
    }

    public bool IsRunning
    {
        get { return _loop is { IsCompleted: false }; }
    }

    public DateTime LastTouchedUtc
    {
        get { return _lastTouchedUtc; }
    }

    public void Touch()
    {
        _lastTouchedUtc = DateTime.UtcNow;
    }

    public void Start()
    {
        lock (_lock)
        {
            if (IsRunning) return;
            _cts = new CancellationTokenSource();
            _loop = Task.Run(() => RunLoopAsync(_cts.Token));
        }
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cts;
        Task? loop;
        lock (_lock)
        {
            cts = _cts;
            loop = _loop;
            _cts = null;
            _loop = null;
        }
        cts?.Cancel();
        if (loop is not null)
        {
            try { await loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (Exception ex) { _logger.Warning(ex, "Director loop ended with error."); }
        }
        cts?.Dispose();
    }

    public ChannelStreamSnapshot Snapshot()
    {
        return new ChannelStreamSnapshot(
            Running: IsRunning,
            ChannelId: _channelId,
            CurrentEntryId: _currentEntryId,
            CurrentEntryTitle: _currentEntryTitle,
            CurrentEntryKind: _currentEntryKind,
            CurrentEntryPartIndex: _currentEntryPartIndex,
            CurrentEntryPartCount: _currentEntryPartCount,
            NextSegmentNumber: _nextSegmentNumber,
            FfmpegPid: _ffmpegPid,
            LastViewerAt: _lastTouchedUtc,
            WillStopAt: _lastTouchedUtc + IdleStopAfter
        );
    }

    private async Task RunLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var sp = scope.ServiceProvider;
                var channels = sp.GetRequiredService<IDataAccess<ChannelEntity>>();
                var media = sp.GetRequiredService<IDataAccess<ChannelMediaEntity>>();
                var libraries = sp.GetRequiredService<IDataAccess<MediaLibraryEntity>>();
                var entries = sp.GetRequiredService<IDataAccess<ScheduledEntryEntity>>();
                var pool = sp.GetRequiredService<IFfmpegPool>();
                var resolver = sp.GetRequiredService<IMediaPathResolver>();

                var channel = channels.GetByKey(_channelId);
                if (channel is null) { await Task.Delay(1000, ct).ConfigureAwait(false); continue; }

                var now = DateTime.UtcNow;
                var current = entries
                    .List()
                    .Where(e => e.ChannelId == _channelId && e.StartAt <= now && e.StartAt.AddSeconds(e.DurationSeconds) > now)
                    .OrderBy(e => e.StartAt)
                    .FirstOrDefault();
                if (current is null) { await Task.Delay(500, ct).ConfigureAwait(false); continue; }

                var mediaEntity = media.GetByKey(current.ChannelMediaId);
                if (mediaEntity is null) { await Task.Delay(500, ct).ConfigureAwait(false); continue; }

                var library = mediaEntity.MediaLibraryId is null
                    ? null
                    : libraries.GetByKey(mediaEntity.MediaLibraryId.Value);

                var sourcePath = await resolver.ResolveAsync(mediaEntity, library!, ct).ConfigureAwait(false);
                var effects = string.IsNullOrWhiteSpace(channel.VideoEffectsJson)
                    ? Array.Empty<ChannelEffect>()
                    : (JsonSerializer.Deserialize<ChannelEffect[]>(channel.VideoEffectsJson) ?? Array.Empty<ChannelEffect>());

                var secondsIntoCurrent = (now - current.StartAt).TotalSeconds;
                var durationLeft = Math.Max(0, current.DurationSeconds - secondsIntoCurrent);
                var vf = EffectCatalog.BuildFilter(effects, channel.OutputWidth, channel.OutputHeight);

                var invocation = FfmpegInvocation.BuildHls(new FfmpegHlsInvocationOptions(
                    FfmpegExecutable: FfmpegExecutable,
                    SourcePath: sourcePath,
                    SkipSeconds: current.MediaOffsetSeconds + secondsIntoCurrent,
                    DurationCapSeconds: durationLeft,
                    VideoFilter: vf,
                    VideoBitrateKbps: channel.OutputVideoBitrateKbps,
                    Fps: DefaultFps,
                    StartSegmentNumber: _nextSegmentNumber,
                    OutputTsOffsetSeconds: _nextTsOffsetSeconds,
                    HlsListSize: HlsListSize,
                    HlsTimeSeconds: HlsTimeSeconds,
                    SegmentFilenamePattern: Path.Combine(_outputDir, "seg%05d.ts"),
                    PlaylistPath: Path.Combine(_outputDir, "index.m3u8")
                ));

                _currentEntryId = current.Id;
                _currentEntryTitle = mediaEntity.Title;
                _currentEntryKind = current.Kind.ToString();
                _currentEntryPartIndex = current.MediaPartIndex;
                _currentEntryPartCount = current.MediaPartCount;

                var handle = await pool.RunAsync(invocation, progress: null, ct).ConfigureAwait(false);
                _ffmpegPid = null;
                var snapshot = await handle.Completion.ConfigureAwait(false);
                _ffmpegPid = null;

                var written = CountSegmentsWrittenSince(_nextSegmentNumber);
                _nextSegmentNumber += written;
                _nextTsOffsetSeconds += durationLeft;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Director loop iteration failed on channel {ChannelId}.", _channelId);
                try { await Task.Delay(1000, ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private int CountSegmentsWrittenSince(int previousStart)
    {
        if (!Directory.Exists(_outputDir)) return 0;
        var files = Directory.GetFiles(_outputDir, "seg*.ts");
        var max = previousStart - 1;
        foreach (var f in files)
        {
            var name = Path.GetFileNameWithoutExtension(f);
            if (name.Length <= 3) continue;
            if (int.TryParse(name.AsSpan(3), out var n) && n > max)
            {
                max = n;
            }
        }
        return Math.Max(0, max - previousStart + 1);
    }
}
