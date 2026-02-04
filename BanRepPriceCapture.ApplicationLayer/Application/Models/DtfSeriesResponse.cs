namespace BanRepPriceCapture.ApplicationLayer.Models;

public sealed record DtfSeriesResponse(
    string Series,
    DateOnly? Start,
    DateOnly? End,
    int Count,
    IReadOnlyList<BanRepSeriesData> Data);
