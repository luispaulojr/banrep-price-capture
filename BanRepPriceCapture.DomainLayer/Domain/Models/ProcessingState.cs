namespace BanRepPriceCapture.DomainLayer.Domain.Models;

public sealed record ProcessingState(
    DateOnly CaptureDate,
    Guid FlowId,
    ProcessingStatus Status,
    DateTime LastUpdatedAt,
    string? ErrorMessage,
    Guid? DownstreamSendId);

public enum ProcessingStatus
{
    Received,
    Processing,
    Persisted,
    Sent,
    Failed
}
