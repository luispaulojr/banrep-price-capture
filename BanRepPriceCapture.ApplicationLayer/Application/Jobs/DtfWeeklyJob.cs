using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Application.Models;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;

namespace BanRepPriceCapture.ApplicationLayer.Application.Jobs;

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
            message: $"start={start} end={end}");
        var data = await client.GetDtfWeeklyAsync(start, end, ct);
        logger.LogInformation(
            method: "DtfWeeklyJob.ExecuteAsync",
            description: "DtfWeeklyJob finalizado.",
            message: $"count={data.Count}");
        return data;
    }
}
