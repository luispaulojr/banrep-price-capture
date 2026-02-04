using BanRepPriceCapture.DtfWeeklyPoc.Domain.Models;

namespace BanRepPriceCapture.DtfWeeklyPoc.Application.Interfaces;

public interface IDtfDailyPriceRepository
{
    Task InsertAsync(
        Guid flowId,
        DateTime dataCapture,
        DateOnly dataPrice,
        DtfDailyPricePayload payload,
        CancellationToken ct);
}
