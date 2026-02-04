using System.Text;
using System.Text.Json;
using BanRepPriceCapture.ApplicationLayer.Interfaces;
using BanRepPriceCapture.DomainLayer.Models;
using BanRepPriceCapture.InfrastructureLayer.Configuration;
using BanRepPriceCapture.ApplicationLayer.Logging;
using BanRepPriceCapture.InfrastructureLayer.Resilience;

namespace BanRepPriceCapture.InfrastructureLayer.Outbound;

public sealed class DtfDailyPayloadSender(
    HttpClient http,
    DtfDailyCaptureSettings settings,
    IStructuredLogger logger,
    IRetryPolicyProvider retryPolicies) : IDtfDailyPayloadSender
{
    private readonly Uri _outboundUri = new(settings.OutboundUrl, UriKind.Absolute);

    public async Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct)
    {
        logger.LogInformation(
            method: "DtfDailyPayloadSender.SendAsync",
            description: "Enviando payload diario.",
            message: $"count={payload.Count}");
        await retryPolicies.ExecuteAsync(async token =>
        {
            var body = JsonSerializer.Serialize(payload);
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await http.PostAsync(_outboundUri, content, token);

            if (!response.IsSuccessStatusCode)
            {
                var message = $"Outbound HTTP falhou. Status={(int)response.StatusCode} {response.ReasonPhrase}.";
                if (IsTransientStatusCode(response.StatusCode))
                {
                    throw new TransientFailureException(message);
                }

                response.EnsureSuccessStatusCode();
            }
        }, RetryPolicyKind.OutboundHttpPost, "DtfDailyPayloadSender.SendAsync", ct);
    }

    private static bool IsTransientStatusCode(System.Net.HttpStatusCode statusCode)
    {
        return statusCode == System.Net.HttpStatusCode.RequestTimeout
            || statusCode == System.Net.HttpStatusCode.TooManyRequests
            || statusCode == System.Net.HttpStatusCode.InternalServerError
            || statusCode == System.Net.HttpStatusCode.BadGateway
            || statusCode == System.Net.HttpStatusCode.ServiceUnavailable
            || statusCode == System.Net.HttpStatusCode.GatewayTimeout;
    }
}
