using BanRepPriceCapture.DomainLayer.Models;

namespace BanRepPriceCapture.ApplicationLayer.Interfaces;

public interface IDtfDailyPayloadSender
{
    Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct);
}
