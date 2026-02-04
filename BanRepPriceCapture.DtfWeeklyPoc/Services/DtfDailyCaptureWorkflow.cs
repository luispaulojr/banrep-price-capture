using BanRepPriceCapture.DtfWeeklyPoc.Models;

namespace BanRepPriceCapture.DtfWeeklyPoc.Services;

public sealed class DtfDailyCaptureWorkflow(
    BanRepSdmxClient client,
    DtfDailyPriceRepository repository,
    DtfDailyPayloadSender sender,
    ILogger<DtfDailyCaptureWorkflow> logger)
{
    public async Task ProcessAsync(Guid flowId, CancellationToken ct)
    {
        logger.LogInformation("Iniciando captura diaria DTF. flow_id={FlowId}", flowId);

        var dailyData = await client.GetDtfDailyAsync(ct: ct);
        var payload = dailyData
            .Select(item => new DtfDailyPricePayload(
                CodAtivo: 123456,
                Data: item.Date.ToString("yyyy-MM-dd"),
                CodPraca: "RBLG",
                CodFeeder: 8,
                CodCampo: 7,
                Preco: item.Value,
                FatorAjuste: 1.0m,
                Previsao: false,
                IsRebook: false))
            .ToList();

        var captureTime = DateTime.UtcNow;
        foreach (var entry in payload)
        {
            var dataPrice = DateOnly.ParseExact(entry.Data, "yyyy-MM-dd");
            await repository.InsertAsync(flowId, captureTime, dataPrice, entry, ct);
        }

        logger.LogInformation("Persistencia concluida. flow_id={FlowId} registros={Count}", flowId, payload.Count);

        await sender.SendAsync(payload, ct);

        logger.LogInformation("Envio HTTP concluido. flow_id={FlowId}", flowId);
    }
}
