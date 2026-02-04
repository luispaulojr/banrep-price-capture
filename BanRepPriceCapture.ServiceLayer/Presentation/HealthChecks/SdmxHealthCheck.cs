using BanRepPriceCapture.ApplicationLayer.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BanRepPriceCapture.ServiceLayer.Presentation.HealthChecks;

public sealed class SdmxHealthCheck(ISdmxClient sdmxClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));

            var referenceDate = DateOnly.FromDateTime(DateTime.UtcNow);
            await sdmxClient.GetDtfDailyAsync(referenceDate, referenceDate, timeout.Token);

            return HealthCheckResult.Healthy("SDMX endpoint reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SDMX endpoint unreachable.", ex);
        }
    }
}
