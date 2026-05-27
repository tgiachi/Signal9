using Microsoft.AspNetCore.SignalR;

namespace SignalNine.Web.Hubs;

/// <summary>
/// SignalR hub for live FFmpeg process status updates. Inbound is empty; events are pushed by
/// <see cref="SignalNine.Web.Services.FfmpegPoolBroadcastService"/>.
/// </summary>
public class FfmpegPoolHub : Hub
{
}
