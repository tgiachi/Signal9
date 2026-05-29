using Microsoft.Extensions.DependencyInjection;
using SignalNine.Persistence.Entities.Channels;
using SignalNine.Persistence.Interfaces;
using SignalNine.Persistence.Types;

namespace SignalNine.Web.Services.Scheduling;

public sealed class SchedulePlannerService
{
    private const int DefaultBumperDurationSeconds = 8;
    private const int DefaultCommercialDurationSeconds = 30;

    private readonly IServiceScopeFactory _scopeFactory;

    public SchedulePlannerService(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    public Task<int> PlanChannelAsync(
        Guid channelId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(0);
    }
}
