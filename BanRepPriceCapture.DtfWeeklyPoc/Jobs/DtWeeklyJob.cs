using BanRepPriceCapture.DtfWeeklyPoc.Models;
using BanRepPriceCapture.DtfWeeklyPoc.Services;

namespace BanRepPriceCapture.DtfWeeklyPoc.Jobs;

public sealed class DtWeeklyJob(BanRepSdmxClient client, ILogger<DtWeeklyJob> logger)
{
    public async Task<List<BanRepSeriesData>> ExecuteAsync(DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        logger.LogInformation("Executando DtWeeklyJob. start={Start}, end={End}", start, end);
        var data = await client.GetDtfDailyAsync(start, end, ct);
        logger.LogInformation("DtWeeklyJob finalizado. count={Count}", data.Count);
        return data;
    }
}