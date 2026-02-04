namespace BanRepPriceCapture.InfrastructureLayer.Resilience;

public enum RetryPolicyKind
{
    SdmxHttp,
    OutboundHttpPost,
    NotificationHttpPost,
    DatabaseConnection,
    RabbitMqConnection
}
