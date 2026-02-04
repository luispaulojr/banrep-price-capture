namespace BanRepPriceCapture.DtfWeeklyPoc.Domain.Models;

public sealed class BanRepSeriesData
{
    public DateOnly Date { get; init; }
    public decimal Value { get; init; }
}
