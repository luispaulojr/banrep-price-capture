using BanRepPriceCapture.InfrastructureLayer.Database;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BanRepPriceCapture.ServiceLayer.Presentation.HealthChecks;

public sealed class DatabaseHealthCheck(IDatabaseConnectionFactory connectionFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory.CreateReadWriteAsync(cancellationToken);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy("Database connection OK.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection failed.", ex);
        }
    }
}
