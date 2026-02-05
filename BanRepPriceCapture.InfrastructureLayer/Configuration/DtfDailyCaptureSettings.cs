using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

namespace BanRepPriceCapture.InfrastructureLayer.Configuration;

public sealed record DtfDailyCaptureSettings : IDtfDailyCaptureSettings
{
    public string ConnectionString { get; init; } = string.Empty;
    public string QueueName { get; init; } = string.Empty;
    public string CsvDirectory { get; init; } = "artifacts/dtf-daily";
    public int PersistenceBatchSize { get; init; } = 200;
    public int PersistenceParallelism { get; init; } = 4;
    public int RequeueNotificationThreshold { get; init; } = 3;
}
