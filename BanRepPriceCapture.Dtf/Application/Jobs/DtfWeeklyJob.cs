using BanRepPriceCapture.Dtf.Application.Interfaces;
using BanRepPriceCapture.Dtf.Domain.Models;
using BanRepPriceCapture.Dtf.Shared.Flow;
using BanRepPriceCapture.Dtf.Shared.Logging;

namespace BanRepPriceCapture.Dtf.Application.Jobs;

public sealed class DtfWeeklyJob(
    ISdmxClient client,
    IStructuredLogger logger,
    IFlowContextAccessor flowContext)
{
    public async Task<List<BanRepSeriesData>> ExecuteAsync(DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        logger.LogInformation(
            method: "DtfWeeklyJob.ExecuteAsync",
            description: "Executando DtfWeeklyJob.",
            message: $"start={start} end={end} flow_id={flowContext.FlowId}");
        var data = await client.GetDtfWeeklyAsync(start, end, ct);
        logger.LogInformation(
            method: "DtfWeeklyJob.ExecuteAsync",
            description: "DtfWeeklyJob finalizado.",
            message: $"count={data.Count} flow_id={flowContext.FlowId}");
        return data;
    }
}
