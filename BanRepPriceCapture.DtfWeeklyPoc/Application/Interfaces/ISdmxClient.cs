using BanRepPriceCapture.DtfWeeklyPoc.Domain.Models;

namespace BanRepPriceCapture.DtfWeeklyPoc.Application.Interfaces;

public interface ISdmxClient
{
    Task<List<BanRepSeriesData>> GetDtfDailyAsync(
        DateOnly? start = null,
        DateOnly? end = null,
        CancellationToken ct = default);

    Task<List<BanRepSeriesData>> GetDtfWeeklyAsync(
        DateOnly? start = null,
        DateOnly? end = null,
        CancellationToken ct = default);
}
