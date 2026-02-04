using System.Net;
using BanRepPriceCapture.ApplicationLayer.Application.Notifications;
using BanRepPriceCapture.ApplicationLayer.Logging;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Notifications;

public sealed class NotificationServiceStub(IStructuredLogger logger) : INotificationService
{
    public HttpStatusCode NotifyInfo(NotificationPayload payload)
    {
        logger.LogWarning(
            method: "NotificationService.NotifyInfo",
            description: "Servico de notificacao nao configurado.",
            message: payload.Title);

        return HttpStatusCode.NotImplemented;
    }

    public HttpStatusCode NotifyWarn(NotificationPayload payload)
    {
        logger.LogWarning(
            method: "NotificationService.NotifyWarn",
            description: "Servico de notificacao nao configurado.",
            message: payload.Title);

        return HttpStatusCode.NotImplemented;
    }

    public HttpStatusCode NotifyError(NotificationPayload payload, Exception? exception = null)
    {
        logger.LogWarning(
            method: "NotificationService.NotifyError",
            description: "Servico de notificacao nao configurado.",
            message: payload.Title,
            exception: exception);

        return HttpStatusCode.NotImplemented;
    }
}
