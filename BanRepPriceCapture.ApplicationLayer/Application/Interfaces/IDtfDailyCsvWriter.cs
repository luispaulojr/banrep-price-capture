using BanRepPriceCapture.ApplicationLayer.Application.Models;

namespace BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

public interface IDtfDailyCsvWriter
{
    Task WriteAsync(
        IAsyncEnumerable<BanRepSeriesData> observations,
        string filePath,
        CancellationToken ct = default);
}
