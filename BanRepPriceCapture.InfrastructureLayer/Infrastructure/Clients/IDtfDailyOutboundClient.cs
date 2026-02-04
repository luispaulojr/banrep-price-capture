using BanRepPriceCapture.DomainLayer.Models;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Clients;

public interface IDtfDailyOutboundClient
{
    Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct);
}
