namespace BanRepPriceCapture.DtfWeeklyPoc.Models;

public sealed record DtfSeriesResponse(
    string Series,
    DateOnly? Start,
    DateOnly? End,
    int Count,
    IReadOnlyList<BanRepSeriesData> Data);
