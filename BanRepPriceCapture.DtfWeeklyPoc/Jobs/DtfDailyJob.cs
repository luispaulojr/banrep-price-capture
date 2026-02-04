using BanRepPriceCapture.DtfWeeklyPoc.Models;
using BanRepPriceCapture.DtfWeeklyPoc.Services;

namespace BanRepPriceCapture.DtfWeeklyPoc.Jobs;

public sealed class DtfDailyJob(BanRepSdmxClient client, ILogger<DtfDailyJob> logger)
{
    public async Task<List<BanRepSeriesData>> ExecuteAsync(DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        logger.LogInformation("Executando DtfDailyJob. start={Start}, end={End}", start, end);
        var data = await client.GetDtfDailyAsync(start, end, ct);
        logger.LogInformation("DtfDailyJob finalizado. count={Count}", data.Count);
        return data;
    }
}
