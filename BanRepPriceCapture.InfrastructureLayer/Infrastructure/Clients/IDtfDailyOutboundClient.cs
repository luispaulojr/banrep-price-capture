using BanRepPriceCapture.DomainLayer.Domain.Models;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Clients;

public interface IDtfDailyOutboundClient
{
    Task<Guid> SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct);
}
