using BanRepPriceCapture.DomainLayer.Domain.Models;

namespace BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

public interface IProcessingStateRepository
{
    Task CreateStateIfNotExists(DateOnly captureDate, Guid flowId, CancellationToken ct);
    Task UpdateStatus(Guid flowId, ProcessingStatus status, string? errorMessage, CancellationToken ct);
    Task<ProcessingState?> GetLastStatusByCaptureDate(DateOnly captureDate, CancellationToken ct);
    Task<ProcessingState?> GetByFlowId(Guid flowId, CancellationToken ct);
    Task<IReadOnlyList<ProcessingState>> ListFailedOrIncompleteExecutions(CancellationToken ct);
}
