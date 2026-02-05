using BanRepPriceCapture.ApplicationLayer.Application.Models;
using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Application.Workflows;
using BanRepPriceCapture.ApplicationLayer.Exceptions;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;
using BanRepPriceCapture.DomainLayer.Domain.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BanRepPriceCapture.ServiceLayer.Presentation.Endpoints;

public static class DtfEndpoints
{
    public static void MapDtfEndpoints(this WebApplication app)
    {
        app.MapGet("/dtf-daily", async (
            [AsParameters] DtfSeriesRequest request,
            DtfSeriesWorkflow workflow,
            CancellationToken ct) =>
        {
            try
            {
                var response = await workflow.GetDailyAsync(request, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        });

        app.MapPost("/dtf-daily/reprocess", async (
            [AsParameters] DtfDailyReprocessRequest request,
            DtfDailyCaptureWorkflow workflow,
            IProcessingStateRepository stateRepository,
            IFlowContextAccessor flowContext,
            IStructuredLogger logger,
            CancellationToken ct) =>
        {
            if (request.CaptureDate is null && request.FlowId is null)
            {
                return Results.BadRequest(new
                {
                    error = "captureDate ou flowId deve ser informado."
                });
            }

            ProcessingState? state = null;
            if (request.FlowId is not null)
            {
                state = await stateRepository.GetByFlowId(request.FlowId.Value, ct);
            }

            if (state is null && request.CaptureDate is not null)
            {
                state = await stateRepository.GetLastStatusByCaptureDate(request.CaptureDate.Value, ct);
            }

            if (state is null)
            {
                return Results.NotFound(new
                {
                    error = "Execucao nao encontrada para os parametros informados."
                });
            }

            var resolvedFlowId = request.FlowId ?? state.FlowId;
            var resolvedCaptureDate = request.CaptureDate ?? state.CaptureDate;

            flowContext.SetFlowId(resolvedFlowId);
            flowContext.SetCaptureDate(resolvedCaptureDate);

            logger.LogInformation(
                method: "DtfEndpoints.ReprocessDailyAsync",
                description: "Reprocessamento DTF Daily solicitado.",
                message: $"flowId={resolvedFlowId} captureDate={resolvedCaptureDate:yyyy-MM-dd}");

            await workflow.ReprocessAsync(request.CaptureDate, request.FlowId, ct);

            return Results.Accepted(new
            {
                flowId = resolvedFlowId,
                captureDate = resolvedCaptureDate
            });
        });

        app.MapGet("/dtf-weekly", async (
            [AsParameters] DtfSeriesRequest request,
            DtfSeriesWorkflow workflow,
            CancellationToken ct) =>
        {
            try
            {
                var response = await workflow.GetWeeklyAsync(request, ct);
                return Results.Ok(response);
            }
            catch (Exception ex)
            {
                return HandleException(ex);
            }
        });
    }

    private static IResult HandleException(Exception ex)
    {
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

    private sealed record DtfDailyReprocessRequest(DateOnly? CaptureDate, Guid? FlowId);
}
