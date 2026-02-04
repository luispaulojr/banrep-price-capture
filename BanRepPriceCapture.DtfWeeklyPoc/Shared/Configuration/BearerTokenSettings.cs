namespace BanRepPriceCapture.DtfWeeklyPoc.Shared.Configuration;

public sealed record BearerTokenSettings
{
    public string TokenEnvVar { get; init; } = "BANREP_BEARER_TOKEN";
}
