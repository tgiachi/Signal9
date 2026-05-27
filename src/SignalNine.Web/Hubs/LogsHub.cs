using Microsoft.AspNetCore.SignalR;

namespace SignalNine.Web.Hubs;

/// <summary>
/// SignalR hub broadcasting global application log entries to connected operators.
/// The hub itself defines no inbound methods; entries are pushed by
/// <see cref="SignalNine.Web.Services.LogsBroadcastService" /> via <c>IHubContext</c>.
/// </summary>
public class LogsHub : Hub
{
}
