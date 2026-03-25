using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SysproIntegrationApi.Services;

public class SysproHealthCheck : IHealthCheck
{
    private readonly SysproSessionPool _pool;
    private readonly ILogger<SysproHealthCheck> _logger;

    public SysproHealthCheck(
        SysproSessionPool pool,
        ILogger<SysproHealthCheck> logger)
    {
        _pool = pool;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken ct = default)
    {
        try
        {
            var isConnected = await _pool.TestConnectionAsync(ct);
            
            if (isConnected)
            {
                return HealthCheckResult.Healthy("SYSPRO connection is healthy");
            }
            
            return HealthCheckResult.Degraded("SYSPRO connection test failed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return HealthCheckResult.Unhealthy("SYSPRO connection error", ex);
        }
    }
}
