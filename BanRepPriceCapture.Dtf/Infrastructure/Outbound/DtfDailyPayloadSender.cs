using System.Text;
using System.Text.Json;
using BanRepPriceCapture.Dtf.Application.Interfaces;
using BanRepPriceCapture.Dtf.Domain.Models;
using BanRepPriceCapture.Dtf.Shared.Configuration;
using BanRepPriceCapture.Dtf.Shared.Logging;

namespace BanRepPriceCapture.Dtf.Infrastructure.Outbound;

public sealed class DtfDailyPayloadSender(
    HttpClient http,
    DtfDailyCaptureSettings settings,
    IStructuredLogger logger) : IDtfDailyPayloadSender
{
    private readonly Uri _outboundUri = new(settings.OutboundUrl, UriKind.Absolute);

    public async Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct)
    {
        logger.LogInformation(
            method: "DtfDailyPayloadSender.SendAsync",
            description: "Enviando payload diario.",
            message: $"count={payload.Count}");
        var body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(_outboundUri, content, ct);
        response.EnsureSuccessStatusCode();
    }
}
