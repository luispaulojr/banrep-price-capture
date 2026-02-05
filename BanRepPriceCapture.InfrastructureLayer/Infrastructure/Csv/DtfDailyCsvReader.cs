using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.ApplicationLayer.Application.Models;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Csv;

public sealed class DtfDailyCsvReader : IDtfDailyCsvReader
{
    public async IAsyncEnumerable<BanRepSeriesData> ReadAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 8192,
            useAsync: true);
        using var reader = new StreamReader(stream, Encoding.UTF8);

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct);
            if (line is null)
            {
                yield break;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var parts = line.Split(',', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                throw new FormatException($"Linha CSV inválida: '{line}'.");
            }

            if (!DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                throw new FormatException($"Data inválida no CSV: '{parts[0]}'.");
            }

            if (!decimal.TryParse(parts[1], NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                throw new FormatException($"Valor inválido no CSV: '{parts[1]}'.");
            }

            yield return new BanRepSeriesData { Date = date, Value = value };
        }
    }
}
