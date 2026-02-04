using BanRepPriceCapture.DtfWeeklyPoc.Application.Jobs;
using BanRepPriceCapture.DtfWeeklyPoc.Application.Notifications;
using BanRepPriceCapture.DtfWeeklyPoc.Domain.Models;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Clients;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Flow;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Logging;

namespace BanRepPriceCapture.DtfWeeklyPoc.Presentation.Endpoints;

public static class DtfEndpoints
{
    public static void MapDtfEndpoints(this WebApplication app)
    {
        app.MapGet("/dtf-daily", async (
            [AsParameters] DtfSeriesRequest request,
            DtfDailyJob job,
            IStructuredLogger logger,
            IFlowContextAccessor flowContext,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var startDate = ParseDate(request.Start);
            var endDate = ParseDate(request.End);

            try
            {
                var data = await job.ExecuteAsync(startDate, endDate, ct);

                return BuildSeriesResponse(
                    "DTF 90 dias (diario, direto do SDMX)",
                    startDate,
                    endDate,
                    data);
            }
            catch (Exception ex)
            {
                return HandleException(ex, logger, flowContext, notificationService);
            }
        });

        app.MapGet("/dtf-weekly", async (
            [AsParameters] DtfSeriesRequest request,
            DtfWeeklyJob job,
            IStructuredLogger logger,
            IFlowContextAccessor flowContext,
            INotificationService notificationService,
            CancellationToken ct) =>
        {
            var startDate = ParseDate(request.Start);
            var endDate = ParseDate(request.End);

            try
            {
                var data = await job.ExecuteAsync(startDate, endDate, ct);

                return BuildSeriesResponse(
                    "DTF 90 dias (semanal, agregado a partir do SDMX diario)",
                    startDate,
                    endDate,
                    data);
            }
            catch (Exception ex)
            {
                return HandleException(ex, logger, flowContext, notificationService);
            }
        });
    }

    private static DateOnly? ParseDate(string? s)
        => DateOnly.TryParse(s, out var d) ? d : null;

    private static IResult BuildSeriesResponse(
        string series,
        DateOnly? startDate,
        DateOnly? endDate,
        List<BanRepSeriesData> data)
    {
        return Results.Ok(new DtfSeriesResponse(
            series,
            startDate,
            endDate,
            data.Count,
            data));
    }

    private static IResult HandleException(
        Exception ex,
        IStructuredLogger logger,
        IFlowContextAccessor flowContext,
        INotificationService notificationService)
    {
        logger.LogError(
            method: "DtfEndpoints.HandleException",
            description: "Erro ao processar requisicao DTF.",
            message: $"flow_id={flowContext.FlowId}",
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

        return ex switch
        {
            TimeoutException timeout => Results.Problem(
                title: "Timeout ao consultar BanRep SDMX",
                detail: timeout.Message,
                statusCode: StatusCodes.Status504GatewayTimeout),
            BanRepSdmxException sdmx => Results.Problem(
                title: "Erro retornado pelo BanRep SDMX",
                detail: sdmx.Message,
                statusCode: StatusCodes.Status502BadGateway),
            HttpRequestException http => Results.Problem(
                title: "Falha de rede ao consultar BanRep SDMX",
                detail: http.Message,
                statusCode: StatusCodes.Status502BadGateway),
            _ => Results.Problem(
                title: "Erro inesperado",
                detail: ex.Message,
                statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
