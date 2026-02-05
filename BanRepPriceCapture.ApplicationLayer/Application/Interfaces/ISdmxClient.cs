using BanRepPriceCapture.ApplicationLayer.Application.Models;

namespace BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

public interface ISdmxClient
{
    Task<List<BanRepSeriesData>> GetDtfDailyAsync(
        DateOnly? start = null,
        DateOnly? end = null,
        CancellationToken ct = default);

    IAsyncEnumerable<BanRepSeriesData> StreamDtfDailyAsync(
        DateOnly? start = null,
        DateOnly? end = null,
        CancellationToken ct = default);

    Task<List<BanRepSeriesData>> GetDtfWeeklyAsync(
        DateOnly? start = null,
        DateOnly? end = null,
        CancellationToken ct = default);
}
