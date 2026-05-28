using Microsoft.Extensions.Hosting;
using Serilog;
using SignalNine.Core.Data.Config;
using ILogger = Serilog.ILogger;

namespace SignalNine.Web.Services;

internal sealed class WorkSpaceJanitor : BackgroundService
{
    private const string JobsDirName = "jobs";

    private readonly ILogger _logger = Log.ForContext<WorkSpaceJanitor>();
    private readonly WorkSpaceConfig _workspace;
    private readonly TimeSpan _tickInterval;

    public WorkSpaceJanitor(SignalNineConfig config)
        : this(config, TimeSpan.FromHours(1))
    {
    }

    // Test-only overload — allows fast ticks without sleeping for an hour.
    internal WorkSpaceJanitor(SignalNineConfig config, TimeSpan tickInterval)
    {
        ArgumentNullException.ThrowIfNull(config);
        _workspace = config.WorkSpace;
        _tickInterval = tickInterval;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_workspace.OrphanCleanupHours <= 0)
        {
            _logger.Information("WorkSpaceJanitor disabled (OrphanCleanupHours <= 0).");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                RunOnce();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "WorkSpaceJanitor tick failed.");
            }

            try
            {
                await Task.Delay(_tickInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    // Internal so tests can drive a single tick without waiting for the full interval.
    internal void RunOnce()
    {
        var jobsRoot = Path.Combine(_workspace.Path, JobsDirName);
        if (!Directory.Exists(jobsRoot))
        {
            _logger.Debug("Jobs root {Root} does not exist yet — skip.", jobsRoot);
            return;
        }

        var cutoff = DateTime.UtcNow.AddHours(-_workspace.OrphanCleanupHours);
        var scanned = 0;
        var reaped = 0;

        foreach (var dir in Directory.EnumerateDirectories(jobsRoot))
        {
            scanned++;
            try
            {
                var mtime = Directory.GetLastWriteTimeUtc(dir);
                if (mtime < cutoff)
                {
                    Directory.Delete(dir, recursive: true);
                    reaped++;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.Warning(ex, "Failed to reap {Dir}", dir);
            }
        }

        if (scanned > 0)
        {
            _logger.Information("Reaped {Reaped} orphan job dirs (scanned {Scanned})", reaped, scanned);
        }
    }
}
