using Npgsql;

namespace BanRepPriceCapture.InfrastructureLayer.Database;

public interface IDatabaseConnectionFactory
{
    Task<NpgsqlConnection> CreateReadWriteAsync(CancellationToken ct);
    Task<NpgsqlConnection> CreateReadOnlyAsync(CancellationToken ct);
}
