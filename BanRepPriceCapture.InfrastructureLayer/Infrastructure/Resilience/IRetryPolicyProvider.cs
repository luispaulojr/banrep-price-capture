namespace BanRepPriceCapture.InfrastructureLayer.Resilience;

public interface IRetryPolicyProvider
{
    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        RetryPolicyKind kind,
        string method,
        CancellationToken ct);

    Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        RetryPolicyKind kind,
        string method,
        CancellationToken ct);
}
