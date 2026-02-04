namespace BanRepPriceCapture.InfrastructureLayer.Configuration;

public sealed record NotificationServiceSettings
{
    public string BaseUrl { get; init; } = string.Empty;
}
