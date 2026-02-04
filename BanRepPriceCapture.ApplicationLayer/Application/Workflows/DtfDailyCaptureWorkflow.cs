using BanRepPriceCapture.ApplicationLayer.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Notifications;
using BanRepPriceCapture.ApplicationLayer.Models;
using BanRepPriceCapture.DomainLayer.Models;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;

namespace BanRepPriceCapture.ApplicationLayer.Workflows;

public sealed class DtfDailyCaptureWorkflow(
    ISdmxClient client,
    IDtfDailyPriceRepository repository,
    IDtfDailyPayloadSender sender,
    IStructuredLogger logger,
    IFlowContextAccessor flowContext,
    INotificationService notificationService)
{
    public async Task ProcessAsync(CancellationToken ct)
    {
        var flowId = flowContext.FlowId;
        logger.LogInformation(
            method: "DtfDailyCaptureWorkflow.ProcessAsync",
            description: "Iniciando captura diaria DTF.");

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

        logger.LogInformation(
            method: "DtfDailyCaptureWorkflow.ProcessAsync",
            description: "Persistencia concluida.",
            message: $"registros={payload.Count}");

        await sender.SendAsync(payload, ct);

        logger.LogInformation(
            method: "DtfDailyCaptureWorkflow.ProcessAsync",
            description: "Envio HTTP concluido.");
    }

    public void NotifyCritical(Exception exception)
    {
        var flowId = flowContext.FlowId;
        logger.LogCritical(
            method: "DtfDailyCaptureWorkflow.NotifyCritical",
            description: "Falha critica no fluxo diario.",
            exception: exception);

        notificationService.NotifyError(new NotificationPayload
        {
            Title = "Falha critica no fluxo diario DTF",
            Description = exception.Message,
            Feature = "DTF Daily Capture",
            Source = "BanRepPriceCapture",
            CorrelationId = flowId.ToString(),
            TemplateName = "dtf-daily-critical"
        }, exception);
    }
}
