using Microsoft.AspNetCore.SignalR;
using SignalNine.Core.Data.Ffmpeg;
using SignalNine.Core.Interfaces;
using SignalNine.Web.Hubs;

namespace SignalNine.Web.Services;

public partial class FfmpegPoolBroadcastService : IHostedService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to broadcast FFmpeg process change via SignalR.")]
    private static partial void LogBroadcastFailure(ILogger logger, Exception exception);

    private readonly IFfmpegPool _pool;
    private readonly IHubContext<FfmpegPoolHub> _hubContext;
    private readonly ILogger<FfmpegPoolBroadcastService> _logger;
    private EventHandler<FfmpegProcessSnapshot>? _handler;

    public FfmpegPoolBroadcastService(
        IFfmpegPool pool,
        IHubContext<FfmpegPoolHub> hubContext,
        ILogger<FfmpegPoolBroadcastService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(pool);
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(logger);

        _pool = pool;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _handler = OnProcessChanged;
        _pool.ProcessChanged += _handler;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_handler is not null)
        {
            _pool.ProcessChanged -= _handler;
            _handler = null;
        }
        return Task.CompletedTask;
    }

    private void OnProcessChanged(object? sender, FfmpegProcessSnapshot snapshot)
    {
        try
        {
            _ = _hubContext.Clients.All.SendAsync("processChanged", snapshot);
        }
        catch (Exception ex)
        {
            LogBroadcastFailure(_logger, ex);
        }
    }
}
