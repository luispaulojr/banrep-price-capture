using BanRepPriceCapture.DomainLayer.Models;

namespace BanRepPriceCapture.InfrastructureLayer.Clients;

public interface IDtfDailyOutboundClient
{
    Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct);
}
