using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Application.Models;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;

namespace BanRepPriceCapture.ApplicationLayer.Application.Jobs;

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
            message: $"start={start} end={end}");
        var data = await client.GetDtfDailyAsync(start, end, ct);
        logger.LogInformation(
            method: "DtfDailyJob.ExecuteAsync",
            description: "DtfDailyJob finalizado.",
            message: $"count={data.Count}");
        return data;
    }
}
