using BanRepPriceCapture.Dtf.Domain.Models;

namespace BanRepPriceCapture.Dtf.Application.Interfaces;

public interface IDtfDailyPriceRepository
{
    Task InsertAsync(
        Guid flowId,
        DateTime dataCapture,
        DateOnly dataPrice,
        DtfDailyPricePayload payload,
        CancellationToken ct);
}
