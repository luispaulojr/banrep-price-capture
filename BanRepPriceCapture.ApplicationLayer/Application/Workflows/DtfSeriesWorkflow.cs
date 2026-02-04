using BanRepPriceCapture.ApplicationLayer.Exceptions;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Application.Jobs;
using BanRepPriceCapture.ApplicationLayer.Logging;
using BanRepPriceCapture.ApplicationLayer.Application.Models;
using BanRepPriceCapture.ApplicationLayer.Application.Notifications;

namespace BanRepPriceCapture.ApplicationLayer.Application.Workflows;

public sealed class DtfSeriesWorkflow(
    DtfDailyJob dailyJob,
    DtfWeeklyJob weeklyJob,
    IStructuredLogger logger,
    IFlowContextAccessor flowContext,
    INotificationService notificationService)
{
    public Task<DtfSeriesResponse> GetDailyAsync(DtfSeriesRequest request, CancellationToken ct)
        => GetSeriesAsync(
            request,
            ct,
            dailyJob.ExecuteAsync,
            "DTF 90 dias (diario, direto do SDMX)");

    public Task<DtfSeriesResponse> GetWeeklyAsync(DtfSeriesRequest request, CancellationToken ct)
        => GetSeriesAsync(
            request,
            ct,
            weeklyJob.ExecuteAsync,
            "DTF 90 dias (semanal, agregado a partir do SDMX diario)");

    private async Task<DtfSeriesResponse> GetSeriesAsync(
        DtfSeriesRequest request,
        CancellationToken ct,
        Func<DateOnly?, DateOnly?, CancellationToken, Task<List<BanRepSeriesData>>> fetch,
        string series)
    {
        var startDate = ParseDate(request.Start);
        var endDate = ParseDate(request.End);

        try
        {
            var data = await fetch(startDate, endDate, ct);

            return new DtfSeriesResponse(
                series,
                startDate,
                endDate,
                data.Count,
                data);
        }
        catch (Exception ex)
        {
            HandleException(ex);
            throw;
        }
    }

    private static DateOnly? ParseDate(string? s)
        => DateOnly.TryParse(s, out var d) ? d : null;

    private void HandleException(Exception ex)
    {
        logger.LogError(
            method: "DtfSeriesWorkflow.HandleException",
            description: "Erro ao processar requisicao DTF.",
            exception: ex);

        if (ex is not TimeoutException && ex is not BanRepSdmxException && ex is not HttpRequestException)
        {
            notificationService.NotifyError(new NotificationPayload
            {
                Title = "Erro inesperado no endpoint DTF",
                Description = ex.Message,
                Feature = "DTF API",
                Source = "BanRepPriceCapture",
                CorrelationId = flowContext.FlowId.ToString(),
                TemplateName = "dtf-api-error"
            }, ex);
        }
    }
}
