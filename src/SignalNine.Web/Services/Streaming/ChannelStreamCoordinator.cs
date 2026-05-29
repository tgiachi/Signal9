using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SignalNine.Core.Directories;
using SignalNine.Web.Data.Streaming;
using ILogger = Serilog.ILogger;

namespace SignalNine.Web.Services.Streaming;

public sealed class ChannelStreamCoordinator : BackgroundService
{
    private const string StreamsDirectoryName = "streams";
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdleStopAfter = TimeSpan.FromSeconds(60);

    private readonly ILogger _logger = Log.ForContext<ChannelStreamCoordinator>();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DirectoriesConfig _directoriesConfig;
    private readonly ConcurrentDictionary<Guid, ChannelStreamDirector> _directors = new();

    public ChannelStreamCoordinator(IServiceScopeFactory scopeFactory, DirectoriesConfig directoriesConfig)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(directoriesConfig);
        _scopeFactory = scopeFactory;
        _directoriesConfig = directoriesConfig;
    }

    public string OutputDir(Guid channelId)
    {
        return Path.Combine(_directoriesConfig.Root, StreamsDirectoryName, channelId.ToString());
    }

    public ChannelStreamDirector GetOrStart(Guid channelId)
    {
        var director = _directors.GetOrAdd(channelId, id =>
            new ChannelStreamDirector(id, OutputDir(id), _scopeFactory));
        director.Touch();
        director.Start();
        return director;
    }

    public void Touch(Guid channelId)
    {
        if (_directors.TryGetValue(channelId, out var d))
        {
            d.Touch();
        }
    }

    public ChannelStreamSnapshot? GetSnapshot(Guid channelId)
    {
        return _directors.TryGetValue(channelId, out var d) ? d.Snapshot() : null;
    }

    public async Task StopAsync(Guid channelId)
    {
        if (_directors.TryRemove(channelId, out var d))
        {
            await d.StopAsync().ConfigureAwait(false);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                foreach (var pair in _directors.ToArray())
                {
                    if (now - pair.Value.LastTouchedUtc > IdleStopAfter)
                    {
                        await StopAsync(pair.Key).ConfigureAwait(false);
                        _logger.Information("Stream director stopped for idle channel {ChannelId}.", pair.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "ChannelStreamCoordinator tick failed.");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
