namespace BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

public interface IDtfDailyCaptureSettings
{
    string CsvDirectory { get; }
    int PersistenceBatchSize { get; }
    int PersistenceParallelism { get; }
}
