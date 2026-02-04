namespace BanRepPriceCapture.Dtf.Shared.Configuration;

public sealed record DtfDailyCaptureSettings
{
    public string ConnectionString { get; init; } = string.Empty;
    public string OutboundUrl { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
    public string TableName { get; init; } = "dtf_daily_prices";
}
