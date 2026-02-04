using Npgsql;

namespace BanRepPriceCapture.Dtf.Infrastructure.Database;

public interface IDatabaseConnectionFactory
{
    Task<NpgsqlConnection> CreateReadWriteAsync(CancellationToken ct);
    Task<NpgsqlConnection> CreateReadOnlyAsync(CancellationToken ct);
}
