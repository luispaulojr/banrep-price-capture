namespace BanRepPriceCapture.InfrastructureLayer.Configuration;

public sealed record DtfDailyCaptureSettings
{
    public string ConnectionString { get; init; } = string.Empty;
    public string OutboundUrl { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
}
