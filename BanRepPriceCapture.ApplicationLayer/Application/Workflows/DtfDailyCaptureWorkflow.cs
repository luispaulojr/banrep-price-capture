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
    IDtfDailyCaptureSettings settings,
    IDtfDailyCsvWriter csvWriter,
    IDtfDailyCsvReader csvReader,
    IProcessingStateRepository stateRepository,
    IDtfDailyPayloadSender sender,
    IArtifactStorageService artifactStorageService,
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

        await ExecuteAsync(
            flowId,
            captureDate,
            allowSkipCompleted: true,
            usePersistedPayload: false,
            ct);
    }

    public async Task ReprocessAsync(DateOnly? captureDate, Guid? flowId, CancellationToken ct)
    {
        if (captureDate is null && flowId is null)
        {
            throw new ArgumentException("CaptureDate ou FlowId devem ser informados.");
        }

        ProcessingState? state = null;
        if (flowId is not null)
        {
            state = await stateRepository.GetByFlowId(flowId.Value, ct);
        }

        if (state is null && captureDate is not null)
        {
            state = await stateRepository.GetLastStatusByCaptureDate(captureDate.Value, ct);
        }

        var resolvedFlowId = flowId ?? state?.FlowId ?? Guid.NewGuid();
        var resolvedCaptureDate = captureDate
            ?? state?.CaptureDate
            ?? flowContext.CaptureDate
            ?? throw new InvalidOperationException("CaptureDate nao configurado no contexto.");

        flowContext.SetFlowId(resolvedFlowId);
        flowContext.SetCaptureDate(resolvedCaptureDate);

        await ExecuteAsync(
            resolvedFlowId,
            resolvedCaptureDate,
            allowSkipCompleted: false,
            usePersistedPayload: true,
            ct);
    }

    private async Task ExecuteAsync(
        Guid flowId,
        DateOnly captureDate,
        bool allowSkipCompleted,
        bool usePersistedPayload,
        CancellationToken ct)
    {
        var csvPath = BuildCsvPath(flowId, captureDate);
        if (allowSkipCompleted)
        {
            var lastState = await stateRepository.GetLastStatusByCaptureDate(captureDate, ct);
            if (lastState is not null && IsCompletedState(lastState))
            {
                logger.LogInformation(
                    method: "DtfDailyCaptureWorkflow.ProcessAsync",
                    description: "Execucao ja enviada, ignorando processamento.",
                    message: $"flowId={lastState.FlowId} captureDate={captureDate:yyyy-MM-dd}");
                return;
            }
        }

        await stateRepository.CreateStateIfNotExists(captureDate, flowId, ct);
        await stateRepository.UpdateStatus(flowId, ProcessingStatus.Processing, null, ct);

        try
        {
            IReadOnlyList<DtfDailyPricePayload> payload;
            var reusedPayload = false;

            if (usePersistedPayload)
            {
                if (File.Exists(csvPath))
                {
                    payload = await ReadPayloadsFromCsvAsync(csvPath, ct);
                    reusedPayload = true;
                }
                else
                {
                    var storedPayload = await repository.GetPayloadsByFlowId(flowId, ct);
                    if (storedPayload.Count > 0)
                    {
                        payload = storedPayload;
                        reusedPayload = true;
                    }
                    else
                    {
                        payload = await FetchFromClientAsync(flowId, captureDate, csvPath, ct);
                    }
                }
            }
            else
            {
                payload = await FetchFromClientAsync(flowId, captureDate, csvPath, ct);
            }

            if (payload.Count == 0)
            {
                logger.LogInformation(
                    method: "DtfDailyCaptureWorkflow.ProcessAsync",
                    description: "Nenhum dado encontrado para processamento.",
                    message: $"flowId={flowId} captureDate={captureDate:yyyy-MM-dd}");
            }

            if (!reusedPayload)
            {
                await PersistPayloadsAsync(flowId, payload, ct);

                logger.LogInformation(
                    method: "DtfDailyCaptureWorkflow.ProcessAsync",
                    description: "Persistencia concluida.",
                    message: $"registros={payload.Count}");
            }
            else
            {
                logger.LogInformation(
                    method: "DtfDailyCaptureWorkflow.ProcessAsync",
                    description: "Reprocessamento usando dados persistidos.",
                    message: $"registros={payload.Count} flowId={flowId}");
            }

            await TryUploadCsvAsync(flowId, captureDate, csvPath, ct);

            await stateRepository.UpdateStatus(flowId, ProcessingStatus.Persisted, null, ct);

            await sender.SendAsync(payload, ct);

            await stateRepository.UpdateStatus(flowId, ProcessingStatus.Sent, null, ct);

            logger.LogInformation(
                method: "DtfDailyCaptureWorkflow.ProcessAsync",
                description: "Envio HTTP concluido.");
        }
        catch (Exception ex)
        {
            await stateRepository.UpdateStatus(flowId, ProcessingStatus.Failed, ex.ToString(), ct);
            throw;
        }
    }

    private async Task<IReadOnlyList<DtfDailyPricePayload>> FetchFromClientAsync(
        Guid flowId,
        DateOnly captureDate,
        string csvPath,
        CancellationToken ct)
    {
        var payloads = new List<DtfDailyPricePayload>();

        var observations = TapObservationsAsync(
            client.StreamDtfDailyAsync(ct: ct),
            payloads,
            ct);

        await csvWriter.WriteAsync(observations, csvPath, ct);

        logger.LogInformation(
            method: "DtfDailyCaptureWorkflow.FetchFromClientAsync",
            description: "CSV gerado com observacoes SDMX.",
            message: $"arquivo={csvPath}");

        return payloads;
    }

    private async Task TryUploadCsvAsync(
        Guid flowId,
        DateOnly captureDate,
        string csvPath,
        CancellationToken ct)
    {
        if (!File.Exists(csvPath))
        {
            logger.LogWarning(
                method: "DtfDailyCaptureWorkflow.TryUploadCsvAsync",
                description: "Arquivo CSV nao encontrado para upload.",
                message: $"flowId={flowId} arquivo={csvPath}");
            return;
        }

        try
        {
            await artifactStorageService.UploadDtfDailyCsvAsync(flowId, captureDate, csvPath, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                method: "DtfDailyCaptureWorkflow.TryUploadCsvAsync",
                description: "Falha ao enviar CSV para armazenamento externo.",
                message: $"flowId={flowId} arquivo={csvPath}",
                exception: ex);
        }
    }

    private async IAsyncEnumerable<BanRepSeriesData> TapObservationsAsync(
        IAsyncEnumerable<BanRepSeriesData> source,
        ICollection<DtfDailyPricePayload> payloads,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in source.WithCancellation(ct))
        {
            payloads.Add(CreatePayload(item));
            yield return item;
        }
    }

    private DtfDailyPricePayload CreatePayload(BanRepSeriesData item)
    {
        return new DtfDailyPricePayload(
            CodAtivo: 123456,
            Data: item.Date.ToString("yyyy-MM-dd"),
            CodPraca: "RBLG",
            CodFeeder: 8,
            CodCampo: 7,
            Preco: item.Value,
            FatorAjuste: 1.0m,
            Previsao: false,
            IsRebook: false);
    }

    private async Task<IReadOnlyList<DtfDailyPricePayload>> ReadPayloadsFromCsvAsync(
        string csvPath,
        CancellationToken ct)
    {
        var payloads = new List<DtfDailyPricePayload>();
        await foreach (var item in csvReader.ReadAsync(csvPath, ct))
        {
            payloads.Add(CreatePayload(item));
        }

        return payloads;
    }

    private async Task PersistPayloadsAsync(
        Guid flowId,
        IReadOnlyList<DtfDailyPricePayload> payloads,
        CancellationToken ct)
    {
        var captureTime = DateTime.UtcNow;
        var batchSize = Math.Max(1, settings.PersistenceBatchSize);
        var parallelism = Math.Max(1, settings.PersistenceParallelism);

        using var semaphore = new SemaphoreSlim(parallelism);
        var tasks = new List<Task>();

        for (var index = 0; index < payloads.Count; index += batchSize)
        {
            var batch = payloads.Skip(index).Take(batchSize).ToArray();
            await semaphore.WaitAsync(ct);
            tasks.Add(ProcessBatchAsync(flowId, captureTime, batch, semaphore, ct));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessBatchAsync(
        Guid flowId,
        DateTime captureTime,
        IReadOnlyList<DtfDailyPricePayload> batch,
        SemaphoreSlim semaphore,
        CancellationToken ct)
    {
        try
        {
            foreach (var entry in batch)
            {
                var dataPrice = DateOnly.ParseExact(entry.Data, "yyyy-MM-dd");
                await repository.InsertAsync(flowId, captureTime, dataPrice, entry, ct);
            }
        }
        finally
        {
            semaphore.Release();
        }
    }

    private string BuildCsvPath(Guid flowId, DateOnly captureDate)
    {
        var fileName = $"{flowId:N}.csv";
        return Path.Combine(
            settings.CsvDirectory,
            captureDate.ToString("yyyyMMdd"),
            fileName);
    }

    private static bool IsCompletedState(ProcessingState state)
    {
        return state.Status == ProcessingStatus.Sent
            || (state.Status == ProcessingStatus.Persisted && state.DownstreamSendId is not null);
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
