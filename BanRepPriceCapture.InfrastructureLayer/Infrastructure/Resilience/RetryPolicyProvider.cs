using System.Net.Sockets;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;
using Npgsql;
using RabbitMQ.Client.Exceptions;

namespace BanRepPriceCapture.InfrastructureLayer.Resilience;

public sealed class RetryPolicyProvider(
    IStructuredLogger logger,
    IFlowContextAccessor flowContext) : IRetryPolicyProvider
{
    private readonly IReadOnlyDictionary<RetryPolicyKind, RetryPolicy> _policies =
        new Dictionary<RetryPolicyKind, RetryPolicy>
        {
            [RetryPolicyKind.SdmxHttp] = RetryPolicy.Create(3, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)),
            [RetryPolicyKind.OutboundHttpPost] = RetryPolicy.Create(3, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)),
            [RetryPolicyKind.NotificationHttpPost] = RetryPolicy.Create(3, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1)),
            [RetryPolicyKind.DatabaseConnection] = RetryPolicy.Create(3, TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(500)),
            [RetryPolicyKind.RabbitMqConnection] = RetryPolicy.Create(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4))
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
        return ex is TransientFailureException
            || ex is HttpRequestException
            || ex is TimeoutException;
    }

    private static bool IsTransientDatabase(Exception ex)
    {
        return ex is TransientFailureException
            || ex is NpgsqlException
            || ex is TimeoutException
            || ex is SocketException;
    }

    private static bool IsTransientRabbit(Exception ex)
    {
        return ex is TransientFailureException
            || ex is BrokerUnreachableException
            || ex is SocketException;
    }

    private sealed record RetryPolicy(int MaxAttempts, IReadOnlyList<TimeSpan> Delays)
    {
        public static RetryPolicy Create(int maxAttempts, params TimeSpan[] delays)
        {
            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            }

            if (delays.Length != Math.Max(0, maxAttempts - 1))
            {
                throw new ArgumentException("Delays must match maxAttempts - 1.", nameof(delays));
            }

            return new RetryPolicy(maxAttempts, delays);
        }
    }
}
