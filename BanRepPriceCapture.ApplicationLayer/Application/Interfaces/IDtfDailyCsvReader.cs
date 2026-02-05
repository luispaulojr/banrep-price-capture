using BanRepPriceCapture.ApplicationLayer.Application.Models;

namespace BanRepPriceCapture.ApplicationLayer.Application.Interfaces;

public interface IDtfDailyCsvReader
{
    IAsyncEnumerable<BanRepSeriesData> ReadAsync(
        string filePath,
        CancellationToken ct = default);
}
