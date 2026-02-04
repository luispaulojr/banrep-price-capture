using BanRepPriceCapture.DtfWeeklyPoc.Domain.Models;

namespace BanRepPriceCapture.DtfWeeklyPoc.Application.Interfaces;

public interface IDtfDailyPayloadSender
{
    Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct);
}
