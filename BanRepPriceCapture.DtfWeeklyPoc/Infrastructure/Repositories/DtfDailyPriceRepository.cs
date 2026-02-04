using System.Text.Json;
using System.Text.RegularExpressions;
using BanRepPriceCapture.DtfWeeklyPoc.Application.Interfaces;
using BanRepPriceCapture.DtfWeeklyPoc.Domain.Models;
using BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Database;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Configuration;
using Dapper;

namespace BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Repositories;

public sealed class DtfDailyPriceRepository(
    DtfDailyCaptureSettings settings,
    IDatabaseConnectionFactory connectionFactory) : IDtfDailyPriceRepository
{
    private static readonly Regex TableNameRegex = new("^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);

    private readonly string _insertSql = BuildInsertSql(settings.TableName);
    private readonly IDatabaseConnectionFactory _connectionFactory = connectionFactory;

    public async Task InsertAsync(
        Guid flowId,
        DateTime dataCapture,
        DateOnly dataPrice,
        DtfDailyPricePayload payload,
        CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateReadWriteAsync(ct);
        await connection.OpenAsync(ct);

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

    private static string BuildInsertSql(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            throw new ArgumentException("Table name nao configurado.", nameof(tableName));
        }

        if (!TableNameRegex.IsMatch(tableName))
        {
            throw new ArgumentException("Table name invalido.", nameof(tableName));
        }

        return $"""
            insert into "{tableName}" (flow_id, data_capture, data_price, payload)
            select @FlowId, @DataCapture, @DataPrice, @Payload::jsonb
            where not exists (
                select 1
                from "{tableName}"
                where flow_id = @FlowId
                  and data_price = @DataPrice
            );
            """;
    }
}
