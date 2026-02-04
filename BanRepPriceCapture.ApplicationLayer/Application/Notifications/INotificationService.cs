using System.Net;

namespace BanRepPriceCapture.ApplicationLayer.Application.Notifications;

public interface INotificationService
{
    HttpStatusCode NotifyInfo(NotificationPayload payload);
    HttpStatusCode NotifyWarn(NotificationPayload payload);
    HttpStatusCode NotifyError(NotificationPayload payload, Exception? exception = null);
}
