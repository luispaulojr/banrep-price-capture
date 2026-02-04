namespace BanRepPriceCapture.Dtf.Domain.Models;

public sealed record DtfSeriesResponse(
    string Series,
    DateOnly? Start,
    DateOnly? End,
    int Count,
    IReadOnlyList<BanRepSeriesData> Data);
