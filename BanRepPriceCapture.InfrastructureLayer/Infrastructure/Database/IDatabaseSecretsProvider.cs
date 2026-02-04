namespace BanRepPriceCapture.InfrastructureLayer.Database;

public interface IDatabaseSecretsProvider
{
    Task<DatabaseSecrets> GetSecretsAsync(CancellationToken ct);
}
