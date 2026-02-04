using BanRepPriceCapture.DomainLayer.Domain.Models;

namespace BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

public interface IDtfDailyPriceRepository
{
    Task InsertAsync(
        Guid flowId,
        DateTime dataCapture,
        DateOnly dataPrice,
        DtfDailyPricePayload payload,
        CancellationToken ct);
}
