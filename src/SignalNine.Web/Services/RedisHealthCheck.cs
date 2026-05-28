using Microsoft.Extensions.Diagnostics.HealthChecks;
using StackExchange.Redis;

namespace SignalNine.Web.Services;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _mux;

    public RedisHealthCheck(IConnectionMultiplexer mux)
    {
        _mux = mux;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _mux.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis health check failed.", ex);
        }
    }
}
