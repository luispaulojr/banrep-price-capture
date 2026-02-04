using BanRepPriceCapture.Dtf.Domain.Models;

namespace BanRepPriceCapture.Dtf.Application.Interfaces;

public interface IDtfDailyPayloadSender
{
    Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct);
}
