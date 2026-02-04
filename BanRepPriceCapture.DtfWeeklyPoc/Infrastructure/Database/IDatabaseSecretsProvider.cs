namespace BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Database;

public interface IDatabaseSecretsProvider
{
    Task<DatabaseSecrets> GetSecretsAsync(CancellationToken ct);
}
