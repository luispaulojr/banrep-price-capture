using System.Text;
using System.Text.Json;
using BanRepPriceCapture.DtfWeeklyPoc.Models;

namespace BanRepPriceCapture.DtfWeeklyPoc.Services;

public sealed class DtfDailyPayloadSender(HttpClient http, DtfDailyCaptureSettings settings)
{
    private readonly Uri _outboundUri = new(settings.OutboundUrl, UriKind.Absolute);

    public async Task SendAsync(IReadOnlyCollection<DtfDailyPricePayload> payload, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(_outboundUri, content, ct);
        response.EnsureSuccessStatusCode();
    }
}
