using System.Text.Json;
using BanRepPriceCapture.ApplicationLayer.Interfaces;
using BanRepPriceCapture.DomainLayer.Models;
using BanRepPriceCapture.InfrastructureLayer.Database;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Database;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Resilience;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Repositories;

public sealed class DtfDailyPriceRepository(
    IDatabaseConnectionFactory connectionFactory,
    IRetryPolicyProvider retryPolicies) : IDtfDailyPriceRepository
{
    private readonly string _insertSql = SqlQueries.InsertDtfDailyPrice;
    private readonly IDatabaseConnectionFactory _connectionFactory = connectionFactory;

    public async Task InsertAsync(
        Guid flowId,
        DateTime dataCapture,
        DateOnly dataPrice,
        DtfDailyPricePayload payload,
        CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateReadWriteAsync(ct);
        await retryPolicies.ExecuteAsync(
            token => connection.OpenAsync(token),
            RetryPolicyKind.DatabaseConnection,
            "DtfDailyPriceRepository.InsertAsync",
            ct);

        var payloadJson = JsonSerializer.Serialize(payload);
        var parameters = new
        {
            FlowId = flowId,
            DataCapture = dataCapture,
            DataPrice = dataPrice,
            Payload = payloadJson
        };

        var command = new CommandDefinition(_insertSql, parameters, cancellationToken: ct);
        await connection.ExecuteAsync(command);
    }
}
