namespace BanRepPriceCapture.DtfWeeklyPoc.Shared.Configuration;

public sealed record DatabaseSettings
{
    public string DatabaseName { get; init; } = string.Empty;
    public int Port { get; init; } = 5432;
    public bool EnableSsl { get; init; } = false;
}
