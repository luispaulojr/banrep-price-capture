namespace BanRepPriceCapture.Dtf.Domain.Models;

public sealed class BanRepSeriesData
{
    public DateOnly Date { get; init; }
    public decimal Value { get; init; }
}
