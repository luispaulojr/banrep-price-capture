using System.Linq;
using BanRepPriceCapture.ApplicationLayer.Application.Interfaces;
using BanRepPriceCapture.DomainLayer.Domain.Models;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Database;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Resilience;
using Dapper;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Repositories;

public sealed class ProcessingStateRepository(
    IDatabaseConnectionFactory connectionFactory,
    IRetryPolicyProvider retryPolicies) : IProcessingStateRepository
{
    private readonly string _insertSql = SqlQueries.InsertProcessingState;
    private readonly string _updateStatusSql = SqlQueries.UpdateProcessingStateStatus;
    private readonly string _recordSendSql = SqlQueries.RecordProcessingStateSend;
    private readonly string _getByFlowIdSql = SqlQueries.GetProcessingStateByFlowId;
    private readonly string _getLastByCaptureDateSql = SqlQueries.GetLastProcessingStateByCaptureDate;
    private readonly string _listFailedOrIncompleteSql = SqlQueries.ListFailedOrIncompleteExecutions;
    private readonly IDatabaseConnectionFactory _connectionFactory = connectionFactory;

    public async Task CreateStateIfNotExists(DateOnly captureDate, Guid flowId, CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateReadWriteAsync(ct);
        await retryPolicies.ExecuteAsync(
            token => connection.OpenAsync(token),
            RetryPolicyKind.DatabaseConnection,
            "ProcessingStateRepository.CreateStateIfNotExists",
            ct);

        var parameters = new
        {
            CaptureDate = captureDate,
            FlowId = flowId,
            Status = ProcessingStatus.Received.ToString(),
            LastUpdatedAt = DateTime.UtcNow
        };

        var command = new CommandDefinition(_insertSql, parameters, cancellationToken: ct);
        await connection.ExecuteAsync(command);
    }

    public async Task UpdateStatus(Guid flowId, ProcessingStatus status, string? errorMessage, CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateReadWriteAsync(ct);
        await retryPolicies.ExecuteAsync(
            token => connection.OpenAsync(token),
            RetryPolicyKind.DatabaseConnection,
            "ProcessingStateRepository.UpdateStatus",
            ct);

        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            var parameters = new
            {
                FlowId = flowId,
                Status = status.ToString(),
                ErrorMessage = errorMessage,
                LastUpdatedAt = DateTime.UtcNow
            };

            var command = new CommandDefinition(_updateStatusSql, parameters, transaction, cancellationToken: ct);
            await connection.ExecuteAsync(command);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task RecordDownstreamSend(Guid flowId, Guid downstreamSendId, CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateReadWriteAsync(ct);
        await retryPolicies.ExecuteAsync(
            token => connection.OpenAsync(token),
            RetryPolicyKind.DatabaseConnection,
            "ProcessingStateRepository.RecordDownstreamSend",
            ct);

        await using var transaction = await connection.BeginTransactionAsync(ct);
        try
        {
            var parameters = new
            {
                FlowId = flowId,
                DownstreamSendId = downstreamSendId,
                Status = ProcessingStatus.Sent.ToString(),
                ErrorMessage = (string?)null,
                LastUpdatedAt = DateTime.UtcNow
            };

            var command = new CommandDefinition(_recordSendSql, parameters, transaction, cancellationToken: ct);
            await connection.ExecuteAsync(command);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ProcessingState?> GetLastStatusByCaptureDate(DateOnly captureDate, CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateReadOnlyAsync(ct);
        await retryPolicies.ExecuteAsync(
            token => connection.OpenAsync(token),
            RetryPolicyKind.DatabaseConnection,
            "ProcessingStateRepository.GetLastStatusByCaptureDate",
            ct);

        var command = new CommandDefinition(
            _getLastByCaptureDateSql,
            new { CaptureDate = captureDate },
            cancellationToken: ct);
        return await connection.QuerySingleOrDefaultAsync<ProcessingState>(command);
    }

    public async Task<ProcessingState?> GetByFlowId(Guid flowId, CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateReadOnlyAsync(ct);
        await retryPolicies.ExecuteAsync(
            token => connection.OpenAsync(token),
            RetryPolicyKind.DatabaseConnection,
            "ProcessingStateRepository.GetByFlowId",
            ct);

        var command = new CommandDefinition(_getByFlowIdSql, new { FlowId = flowId }, cancellationToken: ct);
        return await connection.QuerySingleOrDefaultAsync<ProcessingState>(command);
    }

    public async Task<IReadOnlyList<ProcessingState>> ListFailedOrIncompleteExecutions(CancellationToken ct)
    {
        await using var connection = await _connectionFactory.CreateReadOnlyAsync(ct);
        await retryPolicies.ExecuteAsync(
            token => connection.OpenAsync(token),
            RetryPolicyKind.DatabaseConnection,
            "ProcessingStateRepository.ListFailedOrIncompleteExecutions",
            ct);

        var command = new CommandDefinition(_listFailedOrIncompleteSql, cancellationToken: ct);
        var results = await connection.QueryAsync<ProcessingState>(command);
        return results.ToList();
    }
}
