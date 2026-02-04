namespace BanRepPriceCapture.Dtf.Shared.Logging;

public interface IStructuredLogger
{
    void LogInformation(string method, string description, string? message = null);
    void LogWarning(string method, string description, string? message = null, Exception? exception = null);
    void LogError(string method, string description, string? message = null, Exception? exception = null);
    void LogCritical(string method, string description, string? message = null, Exception? exception = null);
}
