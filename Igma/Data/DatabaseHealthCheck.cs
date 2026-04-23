using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Igma.Data;

public class DatabaseHealthCheck(IDbConnectionFactory db) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var conn = db.CreateConnection();
            conn.ExecuteScalar<int>("SELECT 1");
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(ex.Message));
        }
    }
}
