using System.Net.Http.Headers;
using BanRepPriceCapture.Dtf.Shared.Configuration;

namespace BanRepPriceCapture.Dtf.Infrastructure.Http;

public sealed class BearerTokenHandler(BearerTokenSettings settings) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = Environment.GetEnvironmentVariable(settings.TokenEnvVar);
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
