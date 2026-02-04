using System.Net.Http.Headers;
using BanRepPriceCapture.InfrastructureLayer.Configuration;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Http;

public sealed class BearerTokenHandler(DtfDailyOutboundServiceSettings settings) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var tokenEnvVar = settings.Authentication.TokenEnvVar;
        var token = string.IsNullOrWhiteSpace(tokenEnvVar)
            ? null
            : Environment.GetEnvironmentVariable(tokenEnvVar);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
