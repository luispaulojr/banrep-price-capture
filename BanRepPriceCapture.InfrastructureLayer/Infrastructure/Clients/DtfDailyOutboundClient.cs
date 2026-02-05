using System.Linq;
using System.Text;
using System.Text.Json;
using BanRepPriceCapture.DomainLayer.Domain.Models;
using BanRepPriceCapture.InfrastructureLayer.Configuration;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Resilience;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Clients;

public sealed class DtfDailyOutboundClient(
    HttpClient http,
    DtfDailyOutboundServiceSettings settings,
    IRetryPolicyProvider retryPolicies) : IDtfDailyOutboundClient
{
    private readonly Uri _outboundUri = new(settings.BaseUrl, UriKind.Absolute);
    private static readonly string[] SendIdHeaderNames =
    [
        "X-Downstream-Send-Id",
        "X-Send-Id",
        "X-Request-Id",
        "X-Correlation-Id"
    ];

    public async Task<Guid> SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct)
    {
        return await retryPolicies.ExecuteAsync(async token =>
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

            return ResolveDownstreamSendId(response);
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

    private static Guid ResolveDownstreamSendId(HttpResponseMessage response)
    {
        foreach (var headerName in SendIdHeaderNames)
        {
            if (TryGetHeaderValue(response, headerName, out var value)
                && Guid.TryParse(value, out var headerGuid))
            {
                return headerGuid;
            }
        }

        return Guid.NewGuid();
    }

    private static bool TryGetHeaderValue(HttpResponseMessage response, string headerName, out string? value)
    {
        if (response.Headers.TryGetValues(headerName, out var headerValues))
        {
            value = headerValues.FirstOrDefault();
            return !string.IsNullOrWhiteSpace(value);
        }

        if (response.Content.Headers.TryGetValues(headerName, out var contentValues))
        {
            value = contentValues.FirstOrDefault();
            return !string.IsNullOrWhiteSpace(value);
        }

        value = null;
        return false;
    }
}
