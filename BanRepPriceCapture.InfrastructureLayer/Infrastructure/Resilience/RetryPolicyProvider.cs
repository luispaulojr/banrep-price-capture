using System.Net.Sockets;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;
using Npgsql;
using RabbitMQ.Client.Exceptions;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Resilience;

public sealed class RetryPolicyProvider(
    IStructuredLogger logger,
    IFlowContextAccessor flowContext) : IRetryPolicyProvider
{
    private readonly IReadOnlyDictionary<RetryPolicyKind, RetryPolicy> _policies =
        new Dictionary<RetryPolicyKind, RetryPolicy>
        {
            [RetryPolicyKind.SdmxHttp] = RetryPolicy.Create(3, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1d)),
            [RetryPolicyKind.OutboundHttpPost] = RetryPolicy.Create(3, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1d)),
            [RetryPolicyKind.NotificationHttpPost] = RetryPolicy.Create(3, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1d)),
            [RetryPolicyKind.DatabaseConnection] = RetryPolicy.Create(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500)),
            [RetryPolicyKind.RabbitMqConnection] = RetryPolicy.Create(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1d), TimeSpan.FromSeconds(2d), TimeSpan.FromSeconds(4d))
        };

    public Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        RetryPolicyKind kind,
        string method,
        CancellationToken ct)
    {
        return ExecuteAsync<object?>(
            async token =>
            {
                await action(token);
                return null;
            },
            kind,
            method,
            ct);
    }

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        RetryPolicyKind kind,
        string method,
        CancellationToken ct)
    {
        var policy = _policies[kind];
        var attempt = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            attempt++;

            try
            {
                return await action(ct);
            }
            catch (Exception ex) when (ShouldRetry(kind, ex, ct) && attempt < policy.MaxAttempts)
            {
                var delay = policy.Delays[attempt - 1];
                LogRetry(method, kind, attempt, delay, ex);
                await Task.Delay(delay, ct);
            }
        }
    }

    private void LogRetry(string method, RetryPolicyKind kind, int attempt, TimeSpan delay, Exception ex)
    {
        var flowId = flowContext.FlowId;
        logger.LogWarning(
            method: method,
            description: "Retry agendado.",
            message: $"flowId={flowId} policy={kind} attempt={attempt} delayMs={delay.TotalMilliseconds}",
            exception: ex);
    }

    private static bool ShouldRetry(RetryPolicyKind kind, Exception ex, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
        {
            return false;
        }

        return kind switch
        {
            RetryPolicyKind.SdmxHttp or RetryPolicyKind.OutboundHttpPost or RetryPolicyKind.NotificationHttpPost => IsTransientHttp(ex),
            RetryPolicyKind.DatabaseConnection => IsTransientDatabase(ex),
            RetryPolicyKind.RabbitMqConnection => IsTransientRabbit(ex),
            _ => false
        };
    }

    private static bool IsTransientHttp(Exception ex)
    {
        return ex is TransientFailureException or HttpRequestException or TimeoutException;
    }

    private static bool IsTransientDatabase(Exception ex)
    {
        return ex is TransientFailureException or NpgsqlException or TimeoutException or SocketException;
    }

    private static bool IsTransientRabbit(Exception ex)
    {
        return ex is TransientFailureException or BrokerUnreachableException or SocketException;
    }

    private sealed record RetryPolicy(int MaxAttempts, IReadOnlyList<TimeSpan> Delays)
    {
        public static RetryPolicy Create(int maxAttempts, params TimeSpan[] delays)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);

            return delays.Length != Math.Max(0, maxAttempts - 1) ? throw new ArgumentException("Delays must match maxAttempts - 1.", nameof(delays)) : new RetryPolicy(maxAttempts, delays);
        }
    }
}
