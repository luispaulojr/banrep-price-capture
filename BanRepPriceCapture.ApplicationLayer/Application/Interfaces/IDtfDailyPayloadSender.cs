using BanRepPriceCapture.DomainLayer.Domain.Models;

namespace BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

public interface IDtfDailyPayloadSender
{
    Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct);
}
