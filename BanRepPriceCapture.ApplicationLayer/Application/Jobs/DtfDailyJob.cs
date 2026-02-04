using BanRepPriceCapture.ApplicationLayer.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Models;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;

namespace BanRepPriceCapture.ApplicationLayer.Jobs;

public sealed class DtfDailyJob(
    ISdmxClient client,
    IStructuredLogger logger,
    IFlowContextAccessor flowContext)
{
    public async Task<List<BanRepSeriesData>> ExecuteAsync(DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        logger.LogInformation(
            method: "DtfDailyJob.ExecuteAsync",
            description: "Executando DtfDailyJob.",
            message: $"start={start} end={end} flow_id={flowContext.FlowId}");
        var data = await client.GetDtfDailyAsync(start, end, ct);
        logger.LogInformation(
            method: "DtfDailyJob.ExecuteAsync",
            description: "DtfDailyJob finalizado.",
            message: $"count={data.Count} flow_id={flowContext.FlowId}");
        return data;
    }
}
