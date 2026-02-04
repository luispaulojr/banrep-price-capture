namespace BanRepPriceCapture.ApplicationLayer.Notifications;

public class NotificationPayload
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Feature { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
}
