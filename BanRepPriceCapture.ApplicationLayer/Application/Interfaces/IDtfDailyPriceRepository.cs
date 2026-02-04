using BanRepPriceCapture.DomainLayer.Models;

namespace BanRepPriceCapture.ApplicationLayer.Interfaces;

public interface IDtfDailyPriceRepository
{
    Task InsertAsync(
        Guid flowId,
        DateTime dataCapture,
        DateOnly dataPrice,
        DtfDailyPricePayload payload,
        CancellationToken ct);
}
