using BanRepPriceCapture.ApplicationLayer.Application.Models;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Csv;
using Xunit;

namespace BanRepPriceCapture.TestLayer;

public sealed class CsvProcessingTests
{
    [Fact]
    public async Task CsvWriterAndReader_RoundTripObservations()
    {
        var writer = new DtfDailyCsvWriter();
        var reader = new DtfDailyCsvReader();
        var tempFile = Path.Combine(Path.GetTempPath(), $"dtf-{Guid.NewGuid():N}.csv");

        try
        {
            var observations = new[]
            {
                new BanRepSeriesData { Date = new DateOnly(2024, 8, 16), Value = 10.751m },
                new BanRepSeriesData { Date = new DateOnly(2024, 8, 17), Value = 10.752m }
            };

            await writer.WriteAsync(ToAsyncEnumerable(observations), tempFile);

            var result = new List<BanRepSeriesData>();
            await foreach (var item in reader.ReadAsync(tempFile))
            {
                result.Add(item);
            }

            Assert.Equal(2, result.Count);
            Assert.Equal(observations[0].Date, result[0].Date);
            Assert.Equal(observations[0].Value, result[0].Value);
            Assert.Equal(observations[1].Date, result[1].Date);
            Assert.Equal(observations[1].Value, result[1].Value);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static async IAsyncEnumerable<BanRepSeriesData> ToAsyncEnumerable(
        IEnumerable<BanRepSeriesData> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
