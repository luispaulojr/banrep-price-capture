using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace BanRepPriceCapture.ServiceLayer.Presentation.HealthChecks;

public sealed class RabbitMqHealthCheck(IConnectionFactory connectionFactory) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = connectionFactory.CreateConnection();

            return Task.FromResult(
                connection.IsOpen
                    ? HealthCheckResult.Healthy("RabbitMQ connection OK.")
                    : HealthCheckResult.Unhealthy("RabbitMQ connection closed."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ connection failed.", ex));
        }
    }
}
