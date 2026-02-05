using BanRepPriceCapture.DomainLayer.Domain.Models;

namespace BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

public interface IDtfDailyPayloadSender
{
    Task<Guid> SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct);
}
