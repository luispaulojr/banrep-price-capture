namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Database;

public interface IDatabaseSecretsProvider
{
    Task<DatabaseSecrets> GetSecretsAsync(CancellationToken ct);
}
