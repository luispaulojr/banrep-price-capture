namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Resilience;

public enum RetryPolicyKind
{
    SdmxHttp,
    OutboundHttpPost,
    NotificationHttpPost,
    DatabaseConnection,
    RabbitMqConnection
}
