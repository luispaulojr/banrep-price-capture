using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.DomainLayer.Domain.Models;
using BanRepPriceCapture.ApplicationLayer.Logging;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Clients;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Outbound;

public sealed class DtfDailyPayloadSender(
    IDtfDailyOutboundClient outboundClient,
    IStructuredLogger logger) : IDtfDailyPayloadSender
{
    public async Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct)
    {
        logger.LogInformation(
            method: "DtfDailyPayloadSender.SendAsync",
            description: "Enviando payload diario.",
            message: $"count={payload.Count}");
        await outboundClient.SendAsync(payload, ct);
    }
}
