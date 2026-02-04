namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Database;

public sealed record DatabaseSecrets(
    string Host,
    string HostReadOnly,
    string Username,
    string Password);
