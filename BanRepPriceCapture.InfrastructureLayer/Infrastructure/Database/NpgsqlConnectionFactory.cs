using BanRepPriceCapture.InfrastructureLayer.Configuration;
using Npgsql;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Database;

public sealed class NpgsqlConnectionFactory(
    DatabaseSettings settings,
    IDatabaseSecretsProvider secretsProvider) : IDatabaseConnectionFactory
{
    public async Task<NpgsqlConnection> CreateReadWriteAsync(CancellationToken ct)
    {
        var secrets = await secretsProvider.GetSecretsAsync(ct);
        var connectionString = BuildConnectionString(secrets.Host, secrets.Username, secrets.Password);
        return new NpgsqlConnection(connectionString);
    }

    public async Task<NpgsqlConnection> CreateReadOnlyAsync(CancellationToken ct)
    {
        var secrets = await secretsProvider.GetSecretsAsync(ct);
        var connectionString = BuildConnectionString(secrets.HostReadOnly, secrets.Username, secrets.Password);
        return new NpgsqlConnection(connectionString);
    }

    private string BuildConnectionString(string host, string username, string password)
    {
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = settings.Port,
            Database = settings.DatabaseName,
            Username = username,
            Password = password,
            SslMode = settings.EnableSsl ? SslMode.Require : SslMode.Disable,
            TrustServerCertificate = settings.EnableSsl
        };

        return builder.ConnectionString;
    }
}
