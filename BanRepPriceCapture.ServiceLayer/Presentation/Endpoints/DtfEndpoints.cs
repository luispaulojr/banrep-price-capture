using BanRepPriceCapture.ApplicationLayer.Application.Models;
using BanRepPriceCapture.ApplicationLayer.Application.Workflows;
using BanRepPriceCapture.ApplicationLayer.Exceptions;
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
}
