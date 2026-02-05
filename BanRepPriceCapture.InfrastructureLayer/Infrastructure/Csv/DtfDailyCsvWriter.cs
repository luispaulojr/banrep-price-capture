using System.Globalization;
using System.Text;
using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Application.Models;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Csv;

public sealed class DtfDailyCsvWriter : IDtfDailyCsvWriter
{
    public async Task WriteAsync(
        IAsyncEnumerable<BanRepSeriesData> observations,
        string filePath,
        CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var stream = new FileStream(
            filePath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 8192,
            useAsync: true);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false));

        await foreach (var observation in observations.WithCancellation(ct))
        {
            var line = string.Join(
                ',',
                observation.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                observation.Value.ToString(CultureInfo.InvariantCulture));
            await writer.WriteLineAsync(line.AsMemory(), ct);
        }
    }
}
