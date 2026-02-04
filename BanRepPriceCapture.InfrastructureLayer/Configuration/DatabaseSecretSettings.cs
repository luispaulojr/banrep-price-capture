namespace BanRepPriceCapture.InfrastructureLayer.Configuration;

public sealed record DatabaseSecretSettings
{
    public string? SecretId { get; init; }
    public string HostEnvVar { get; init; } = "BANREP_DB_HOST";
    public string HostReadOnlyEnvVar { get; init; } = "BANREP_DB_HOST_RO";
    public string UsernameEnvVar { get; init; } = "BANREP_DB_USERNAME";
    public string PasswordEnvVar { get; init; } = "BANREP_DB_PASSWORD";
}
