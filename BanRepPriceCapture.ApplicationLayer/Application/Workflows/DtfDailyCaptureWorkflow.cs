using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Application.Notifications;
using BanRepPriceCapture.ApplicationLayer.Application.Models;
using BanRepPriceCapture.DomainLayer.Domain.Models;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;

namespace BanRepPriceCapture.ApplicationLayer.Application.Workflows;

public sealed class DtfDailyCaptureWorkflow(
    ISdmxClient client,
    IDtfDailyPriceRepository repository,
    IProcessingStateRepository stateRepository,
    IDtfDailyPayloadSender sender,
    IStructuredLogger logger,
    IFlowContextAccessor flowContext,
    INotificationService notificationService)
{
    public async Task ProcessAsync(CancellationToken ct)
    {
        var flowId = flowContext.FlowId;
        var captureDate = flowContext.CaptureDate
            ?? throw new InvalidOperationException("CaptureDate nao configurado no contexto.");
        logger.LogInformation(
            method: "DtfDailyCaptureWorkflow.ProcessAsync",
            description: "Iniciando captura diaria DTF.");

        await stateRepository.CreateStateIfNotExists(captureDate, flowId, ct);
        var state = await stateRepository.GetByFlowId(flowId, ct);
        if (state is not null && state.Status == ProcessingStatus.Sent)
        {
            logger.LogInformation(
                method: "DtfDailyCaptureWorkflow.ProcessAsync",
                description: "Execucao ja enviada, ignorando processamento.",
                message: $"flowId={flowId}");
            return;
        }

        await stateRepository.UpdateStatus(flowId, ProcessingStatus.Processing, null, ct);

        try
        {
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

            await stateRepository.UpdateStatus(flowId, ProcessingStatus.Persisted, null, ct);

            await sender.SendAsync(payload, ct);

            await stateRepository.UpdateStatus(flowId, ProcessingStatus.Sent, null, ct);

            logger.LogInformation(
                method: "DtfDailyCaptureWorkflow.ProcessAsync",
                description: "Envio HTTP concluido.");
        }
        catch (Exception ex)
        {
            await stateRepository.UpdateStatus(flowId, ProcessingStatus.Failed, ex.Message, ct);
            throw;
        }
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
