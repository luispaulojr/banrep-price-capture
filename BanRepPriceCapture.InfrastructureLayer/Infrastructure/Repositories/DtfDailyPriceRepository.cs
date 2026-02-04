using System.Text.Json;
using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.DomainLayer.Domain.Models;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Database;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Resilience;
using Dapper;

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
