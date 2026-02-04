using System.Text.Json.Serialization;

namespace BanRepPriceCapture.DtfWeeklyPoc.Application.Notifications;

public class NotificationModel
{
    [JsonPropertyName("sendMethod")]
    public int SendMethod { get; set; } = 2;

    [JsonPropertyName("teamsChat")]
    public TeamsChatNotification TeamsChat { get; set; } = new();
}
