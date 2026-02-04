using System.Text;
using System.Text.Json;
using BanRepPriceCapture.DtfWeeklyPoc.Application.Interfaces;
using BanRepPriceCapture.DtfWeeklyPoc.Domain.Models;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Configuration;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Logging;

namespace BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Outbound;

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
