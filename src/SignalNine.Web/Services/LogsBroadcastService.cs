using Microsoft.AspNetCore.SignalR;
using Serilog.Events;
using SignalNine.Core.Logging;
using SignalNine.Web.Data.Logging;
using SignalNine.Web.Hubs;

namespace SignalNine.Web.Services;

/// <summary>
/// Subscribes to the global Serilog <see cref="EventSink" /> and forwards every captured
/// log event to all clients connected to <see cref="LogsHub" /> via the <c>log</c> client method.
/// </summary>
public partial class LogsBroadcastService : IHostedService
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to broadcast log entry via SignalR LogsHub.")]
    private static partial void LogBroadcastFailure(ILogger logger, Exception exception);

    private readonly IHubContext<LogsHub> _hubContext;
    private readonly ILogger<LogsBroadcastService> _logger;
    private EventHandler<LogEventData>? _handler;

    public LogsBroadcastService(IHubContext<LogsHub> hubContext, ILogger<LogsBroadcastService> logger)
    {
        ArgumentNullException.ThrowIfNull(hubContext);
        ArgumentNullException.ThrowIfNull(logger);

        _hubContext = hubContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _handler = OnLogReceived;
        EventSink.OnLogReceived += _handler;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_handler is not null)
        {
            EventSink.OnLogReceived -= _handler;
            _handler = null;
        }

        return Task.CompletedTask;
    }

    private void OnLogReceived(object? sender, LogEventData logEvent)
    {
        try
        {
            var payload = new LogEntryResponse
            {
                Ts = logEvent.Timestamp.UtcDateTime.ToString("O"),
                Level = MapLevel(logEvent.Level),
                Source = logEvent.SourceContext ?? string.Empty,
                Message = logEvent.Message,
                Props = logEvent.Properties.Count == 0 ? null : logEvent.Properties
            };

            _ = _hubContext.Clients.All.SendAsync("log", payload);
        }
        catch (Exception ex)
        {
            LogBroadcastFailure(_logger, ex);
        }
    }

    private static string MapLevel(LogEventLevel level)
    {
        return level switch
        {
            LogEventLevel.Verbose => "debug",
            LogEventLevel.Debug => "debug",
            LogEventLevel.Information => "info",
            LogEventLevel.Warning => "warn",
            LogEventLevel.Error => "error",
            LogEventLevel.Fatal => "error",
            _ => "info"
        };
    }
}
