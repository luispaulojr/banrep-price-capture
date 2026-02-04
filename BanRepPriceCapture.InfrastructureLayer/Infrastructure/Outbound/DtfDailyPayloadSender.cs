using BanRepPriceCapture.ApplicationLayer.Interfaces;
using BanRepPriceCapture.DomainLayer.Models;
using BanRepPriceCapture.ApplicationLayer.Logging;
using BanRepPriceCapture.InfrastructureLayer.Clients;

namespace BanRepPriceCapture.InfrastructureLayer.Outbound;

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
