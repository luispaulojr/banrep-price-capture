namespace BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

public interface IArtifactStorageService
{
    Task UploadDtfDailyCsvAsync(Guid flowId, DateOnly captureDate, string csvPath, CancellationToken ct);
}
