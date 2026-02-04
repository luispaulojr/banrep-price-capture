namespace BanRepPriceCapture.Dtf.Infrastructure.Database;

public interface IDatabaseSecretsProvider
{
    Task<DatabaseSecrets> GetSecretsAsync(CancellationToken ct);
}
