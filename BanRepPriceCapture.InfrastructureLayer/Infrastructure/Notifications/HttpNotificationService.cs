using System.Net;
using System.Text;
using System.Text.Json;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;
using BanRepPriceCapture.ApplicationLayer.Notifications;
using BanRepPriceCapture.InfrastructureLayer.Configuration;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Resilience;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Notifications;

public sealed class HttpNotificationService(
    HttpClient httpClient,
    NotificationServiceSettings settings,
    IStructuredLogger logger,
    IFlowContextAccessor flowContext,
    IRetryPolicyProvider retryPolicies) : INotificationService
{
    private readonly Uri _notifyUri = BuildNotifyUri(settings.BaseUrl);

    public HttpStatusCode NotifyInfo(NotificationPayload payload)
    {
        return NotifyAsync(payload, "HttpNotificationService.NotifyInfo").GetAwaiter().GetResult();
    }

    public HttpStatusCode NotifyWarn(NotificationPayload payload)
    {
        return NotifyAsync(payload, "HttpNotificationService.NotifyWarn").GetAwaiter().GetResult();
    }

    public HttpStatusCode NotifyError(NotificationPayload payload, Exception? exception = null)
    {
        if (exception is not null)
        {
            logger.LogError(
                method: "HttpNotificationService.NotifyError",
                description: "Falha reportada para notificacao.",
                message: exception.Message,
                exception: exception);
        }

        return NotifyAsync(payload, "HttpNotificationService.NotifyError").GetAwaiter().GetResult();
    }

    private async Task<HttpStatusCode> NotifyAsync(NotificationPayload payload, string method)
    {
        payload.CorrelationId = flowContext.FlowId.ToString();

        try
        {
            return await retryPolicies.ExecuteAsync(async token =>
            {
                var body = BuildRequestBody(payload);
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await httpClient.PostAsync(_notifyUri, content, token);

                if (!response.IsSuccessStatusCode)
                {
                    var message = $"Notification HTTP falhou. Status={(int)response.StatusCode} {response.ReasonPhrase}.";
                    if (IsTransientStatusCode(response.StatusCode))
                    {
                        throw new TransientFailureException(message);
                    }

                    logger.LogWarning(
                        method: method,
                        description: "Notification HTTP retornou erro.",
                        message: message);

                    response.EnsureSuccessStatusCode();
                }

                return response.StatusCode;
            }, RetryPolicyKind.NotificationHttpPost, method, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                method: method,
                description: "Falha ao enviar notificacao.",
                message: ex.Message,
                exception: ex);

            throw;
        }
    }

    private static string BuildRequestBody(NotificationPayload payload)
    {
        var model = new NotificationModel
        {
            TeamsChat = new TeamsChatNotification
            {
                CardMessage = JsonSerializer.Serialize(payload)
            }
        };

        return JsonSerializer.Serialize(model);
    }

    private static Uri BuildNotifyUri(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("NotificationService BaseUrl nao configurado.");
        }

        var normalizedBase = baseUrl.TrimEnd('/');
        return new Uri($"{normalizedBase}/notificar", UriKind.Absolute);
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
            || statusCode == HttpStatusCode.TooManyRequests
            || statusCode == HttpStatusCode.InternalServerError
            || statusCode == HttpStatusCode.BadGateway
            || statusCode == HttpStatusCode.ServiceUnavailable
            || statusCode == HttpStatusCode.GatewayTimeout;
    }
}
