using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SignalNine.Web.Services;

public class FreeSqlHealthCheck : IHealthCheck
{
    private readonly IFreeSql _freeSql;

    public FreeSqlHealthCheck(IFreeSql freeSql)
    {
        ArgumentNullException.ThrowIfNull(freeSql);

        _freeSql = freeSql;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _freeSql.Ado.ExecuteScalar("SELECT 1");

            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Database health check failed.", ex));
        }
    }
}
