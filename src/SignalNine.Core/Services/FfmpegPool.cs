using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using SignalNine.Core.Data.Config;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Interfaces;
using SignalNine.Core.Services.Ffmpeg;

namespace SignalNine.Core.Services;

public class FfmpegPool : IFfmpegPool, IDisposable
{
    private readonly IProcessLauncher _launcher;
    private readonly FfmpegPoolConfig _config;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentDictionary<Guid, ProcessEntry> _entries = new();
    private readonly ConcurrentQueue<Guid> _retentionOrder = new();
    private readonly object _retentionLock = new();
    private bool _disposed;

    public event EventHandler<FfmpegProcessSnapshot>? ProcessChanged;

    public FfmpegPool(IProcessLauncher launcher, FfmpegPoolConfig config)
    {
        ArgumentNullException.ThrowIfNull(launcher);
        ArgumentNullException.ThrowIfNull(config);
        ValidateConfig(config);

        _launcher = launcher;
        _config = config;
        _semaphore = new SemaphoreSlim(config.MaxConcurrent, config.MaxConcurrent);
    }

    public async Task<FfprobeResult> ProbeAsync(string inputPath, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(inputPath);

        var invocation = FfmpegInvocation.Probe(_config.FfprobePath, inputPath);
        var stdoutLines = new List<string>();
        var stderrLines = new List<string>();

        var handle = await RunInternalAsync(
            invocation,
            progress: null,
            onStdout: line => { lock (stdoutLines) stdoutLines.Add(line); },
            onStderr: line => { lock (stderrLines) stderrLines.Add(line); },
            ct
        ).ConfigureAwait(false);

        var snapshot = await handle.Completion.ConfigureAwait(false);

        if (snapshot.Status == FfmpegProcessStatusType.Canceled)
        {
            throw new FfmpegCanceledException();
        }

        if (snapshot.Status == FfmpegProcessStatusType.Failed)
        {
            var lastOutput = string.Join('\n', stderrLines.TakeLast(20));
            throw new FfmpegExecutionException(
                $"ffprobe exited with code {snapshot.ExitCode}.",
                snapshot.ExitCode,
                lastOutput
            );
        }

        var json = string.Concat(stdoutLines);
        return FfprobeJsonParser.Parse(json);
    }

    public Task<FfmpegProcessHandle> RunAsync(
        FfmpegInvocation invocation,
        IProgress<FfmpegProgressUpdate>? progress = null,
        CancellationToken ct = default
    )
    {
        ArgumentNullException.ThrowIfNull(invocation);
        return RunInternalAsync(invocation, progress, onStdout: null, onStderr: null, ct);
    }

    public IReadOnlyList<FfmpegProcessSnapshot> List()
    {
        return _entries.Values.Select(e => e.Snapshot()).OrderByDescending(s => s.QueuedAt).ToList();
    }

    public FfmpegProcessSnapshot? Get(Guid id)
    {
        return _entries.TryGetValue(id, out var entry) ? entry.Snapshot() : null;
    }

    public Task<bool> CancelAsync(Guid processId, CancellationToken ct = default)
    {
        if (!_entries.TryGetValue(processId, out var entry)) return Task.FromResult(false);
        if (IsTerminal(entry.Status)) return Task.FromResult(false);

        entry.RequestCancel();
        return Task.FromResult(true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var entry in _entries.Values)
        {
            entry.RequestCancel();
        }

        _semaphore.Dispose();
        GC.SuppressFinalize(this);
    }

    private Task<FfmpegProcessHandle> RunInternalAsync(
        FfmpegInvocation invocation,
        IProgress<FfmpegProgressUpdate>? progress,
        Action<string>? onStdout,
        Action<string>? onStderr,
        CancellationToken ct
    )
    {
        var entry = new ProcessEntry(invocation, _config.OutputBufferLines);
        _entries[entry.Id] = entry;
        EnforceRetention(entry.Id);
        RaiseChanged(entry);

        var completion = ExecuteAsync(entry, progress, onStdout, onStderr, ct);
        var handle = new FfmpegProcessHandle(
            entry.Id,
            completion,
            () => CancelAsync(entry.Id)
        );

        return Task.FromResult(handle);
    }

    private async Task<FfmpegProcessSnapshot> ExecuteAsync(
        ProcessEntry entry,
        IProgress<FfmpegProgressUpdate>? progress,
        Action<string>? onStdout,
        Action<string>? onStderr,
        CancellationToken externalCt
    )
    {
        using var internalCts = entry.CreateInternalCts();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(externalCt, internalCts.Token);

        try
        {
            await _semaphore.WaitAsync(linked.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            entry.Transition(FfmpegProcessStatusType.Canceled);
            RaiseChanged(entry);
            return entry.Snapshot();
        }

        try
        {
            entry.Transition(FfmpegProcessStatusType.Starting);
            RaiseChanged(entry);

            var psi = new ProcessStartInfo
            {
                FileName = entry.Invocation.Executable,
                WorkingDirectory = Path.GetTempPath()
            };
            foreach (var a in entry.Invocation.Arguments)
            {
                psi.ArgumentList.Add(a);
            }

            IRunningProcess proc;
            try
            {
                proc = _launcher.Start(psi);
            }
            catch (FfmpegNotFoundException ex)
            {
                entry.SetError(ex.Message);
                entry.Transition(FfmpegProcessStatusType.Failed);
                RaiseChanged(entry);
                return entry.Snapshot();
            }

            using (proc)
            {
                entry.SetPid(proc.Pid);
                entry.Transition(FfmpegProcessStatusType.Running);
                RaiseChanged(entry);

                proc.StdoutLineReceived += (_, line) =>
                {
                    onStdout?.Invoke(line);
                    entry.AppendOutput(line);
                    if (TryParseProgress(entry, line, out var update) && update is not null)
                    {
                        entry.SetProgress(update);
                        progress?.Report(update);
                        RaiseChanged(entry);
                    }
                };
                proc.StderrLineReceived += (_, line) =>
                {
                    onStderr?.Invoke(line);
                    entry.AppendOutput(line);
                };

                using var killReg = linked.Token.Register(() => KillWithGrace(proc, _config.KillGraceSeconds));

                int exitCode;
                try
                {
                    exitCode = await proc.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    entry.SetError(ex.Message);
                    entry.Transition(FfmpegProcessStatusType.Failed);
                    RaiseChanged(entry);
                    return entry.Snapshot();
                }

                entry.SetExitCode(exitCode);

                if (linked.IsCancellationRequested)
                {
                    entry.Transition(FfmpegProcessStatusType.Canceled);
                }
                else if (exitCode == 0)
                {
                    entry.Transition(FfmpegProcessStatusType.Completed);
                }
                else
                {
                    entry.Transition(FfmpegProcessStatusType.Failed);
                }

                RaiseChanged(entry);
                return entry.Snapshot();
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static void KillWithGrace(IRunningProcess proc, int graceSeconds)
    {
        try
        {
            proc.Kill(entireProcessTree: false);
        }
        catch
        {
            // best effort
        }

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(graceSeconds)).ConfigureAwait(false);
                proc.Kill(entireProcessTree: true);
            }
            catch
            {
                // best effort
            }
        });
    }

    private static bool TryParseProgress(ProcessEntry entry, string line, out FfmpegProgressUpdate? update)
    {
        update = null;
        var eq = line.IndexOf('=');
        if (eq <= 0) return false;

        var key = line[..eq].Trim();
        var value = line[(eq + 1)..].Trim();
        entry.SetProgressField(key, value);

        if (key == "progress")
        {
            update = entry.BuildProgress();
            return update is not null;
        }
        return false;
    }

    private void EnforceRetention(Guid newId)
    {
        _retentionOrder.Enqueue(newId);
        lock (_retentionLock)
        {
            while (_retentionOrder.Count > _config.RegistryRetention)
            {
                if (_retentionOrder.TryDequeue(out var old))
                {
                    if (_entries.TryGetValue(old, out var entry) && IsTerminal(entry.Status))
                    {
                        _entries.TryRemove(old, out _);
                    }
                    else
                    {
                        _retentionOrder.Enqueue(old);
                        break;
                    }
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void RaiseChanged(ProcessEntry entry)
    {
        var snapshot = entry.Snapshot();
        try
        {
            ProcessChanged?.Invoke(this, snapshot);
        }
        catch
        {
            // Subscriber errors must not break the pool
        }
    }

    private static bool IsTerminal(FfmpegProcessStatusType s)
    {
        return s is FfmpegProcessStatusType.Completed
            or FfmpegProcessStatusType.Failed
            or FfmpegProcessStatusType.Canceled;
    }

    private static void ValidateConfig(FfmpegPoolConfig cfg)
    {
        if (cfg.MaxConcurrent < 1) throw new ArgumentOutOfRangeException(nameof(cfg.MaxConcurrent));
        if (cfg.KillGraceSeconds < 1) throw new ArgumentOutOfRangeException(nameof(cfg.KillGraceSeconds));
        if (cfg.OutputBufferLines < 1) throw new ArgumentOutOfRangeException(nameof(cfg.OutputBufferLines));
        if (cfg.RegistryRetention < 1) throw new ArgumentOutOfRangeException(nameof(cfg.RegistryRetention));
    }

    private sealed class ProcessEntry
    {
        private readonly object _lock = new();
        private readonly Queue<string> _output;
        private readonly int _maxOutput;
        private readonly Dictionary<string, string> _progressFields = new();
        private CancellationTokenSource? _cts;

        public ProcessEntry(FfmpegInvocation invocation, int maxOutput)
        {
            Id = Guid.NewGuid();
            Invocation = invocation;
            QueuedAt = DateTime.UtcNow;
            Status = FfmpegProcessStatusType.Queued;
            _output = new Queue<string>();
            _maxOutput = maxOutput;
        }

        public Guid Id { get; }
        public FfmpegInvocation Invocation { get; }
        public DateTime QueuedAt { get; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? EndedAt { get; private set; }
        public FfmpegProcessStatusType Status { get; private set; }
        public int? Pid { get; private set; }
        public int? ExitCode { get; private set; }
        public string? Error { get; private set; }
        public FfmpegProgressUpdate? LastProgress { get; private set; }

        public CancellationTokenSource CreateInternalCts()
        {
            _cts = new CancellationTokenSource();
            return _cts;
        }

        public void RequestCancel()
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // ignore
            }
        }

        public void SetPid(int pid)
        {
            Pid = pid;
        }

        public void SetExitCode(int code)
        {
            ExitCode = code;
        }

        public void SetError(string error)
        {
            Error = error;
        }

        public void Transition(FfmpegProcessStatusType to)
        {
            lock (_lock)
            {
                Status = to;
                if (to == FfmpegProcessStatusType.Running && StartedAt is null)
                {
                    StartedAt = DateTime.UtcNow;
                }
                if (to is FfmpegProcessStatusType.Completed
                       or FfmpegProcessStatusType.Failed
                       or FfmpegProcessStatusType.Canceled)
                {
                    EndedAt = DateTime.UtcNow;
                }
            }
        }

        public void AppendOutput(string line)
        {
            lock (_lock)
            {
                _output.Enqueue(line);
                while (_output.Count > _maxOutput) _output.Dequeue();
            }
        }

        public void SetProgress(FfmpegProgressUpdate update)
        {
            LastProgress = update;
        }

        public void SetProgressField(string key, string value)
        {
            lock (_lock)
            {
                _progressFields[key] = value;
            }
        }

        public FfmpegProgressUpdate? BuildProgress()
        {
            lock (_lock)
            {
                long frame = 0; double fps = 0; double speed = 0; long bytes = 0;
                TimeSpan outTime = TimeSpan.Zero;

                if (_progressFields.TryGetValue("frame", out var fStr))
                {
                    long.TryParse(fStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out frame);
                }
                if (_progressFields.TryGetValue("fps", out var fpsStr))
                {
                    double.TryParse(fpsStr, NumberStyles.Float, CultureInfo.InvariantCulture, out fps);
                }
                if (_progressFields.TryGetValue("out_time_us", out var us)
                    && long.TryParse(us, NumberStyles.Integer, CultureInfo.InvariantCulture, out var usValue))
                {
                    outTime = TimeSpan.FromMilliseconds(usValue / 1000.0);
                }
                if (_progressFields.TryGetValue("total_size", out var sizeStr))
                {
                    long.TryParse(sizeStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out bytes);
                }
                if (_progressFields.TryGetValue("speed", out var speedStr))
                {
                    var clean = speedStr.TrimEnd('x', ' ', '\t');
                    double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out speed);
                }

                return new FfmpegProgressUpdate(frame, fps, outTime, speed, bytes);
            }
        }

        public FfmpegProcessSnapshot Snapshot()
        {
            lock (_lock)
            {
                return new FfmpegProcessSnapshot(
                    Id,
                    Pid,
                    Invocation.Executable,
                    Invocation.Arguments,
                    Status,
                    QueuedAt,
                    StartedAt,
                    EndedAt,
                    ExitCode,
                    LastProgress,
                    _output.ToArray(),
                    Error
                );
            }
        }
    }
}
