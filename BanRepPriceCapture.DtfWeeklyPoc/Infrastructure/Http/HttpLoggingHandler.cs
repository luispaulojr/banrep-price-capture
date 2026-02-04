using BanRepPriceCapture.DtfWeeklyPoc.Shared.Logging;

namespace BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Http;

public sealed class HttpLoggingHandler(IStructuredLogger logger) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            method: "HttpClient.SendAsync",
            description: "Requisicao HTTP iniciada.",
            message: $"{request.Method} {request.RequestUri}");

        var response = await base.SendAsync(request, cancellationToken);

        logger.LogInformation(
            method: "HttpClient.SendAsync",
            description: "Resposta HTTP recebida.",
            message: $"{request.Method} {request.RequestUri} status={(int)response.StatusCode}");

        return response;
    }
}
