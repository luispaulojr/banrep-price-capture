using BanRepPriceCapture.DtfWeeklyPoc.Shared.Flow;
using Microsoft.Extensions.Logging;

namespace BanRepPriceCapture.DtfWeeklyPoc.Shared.Logging;

public sealed class StructuredLogger(
    ILogger<StructuredLogger> logger,
    IFlowContextAccessor flowContext) : IStructuredLogger
{
    public void LogInformation(string method, string description, string? message = null)
        => Log(LogLevel.Information, method, description, message);

    public void LogWarning(string method, string description, string? message = null, Exception? exception = null)
        => Log(LogLevel.Warning, method, description, message, exception);

    public void LogError(string method, string description, string? message = null, Exception? exception = null)
        => Log(LogLevel.Error, method, description, message, exception);

    public void LogCritical(string method, string description, string? message = null, Exception? exception = null)
        => Log(LogLevel.Critical, method, description, message, exception);

    private void Log(
        LogLevel level,
        string method,
        string description,
        string? message = null,
        Exception? exception = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["Level"] = level.ToString(),
            ["FlowId"] = flowContext.FlowId,
            ["Method"] = method,
            ["Description"] = description
        };

        if (!string.IsNullOrWhiteSpace(message))
        {
            payload["Message"] = message;
        }

        if (exception is not null)
        {
            payload["Exception"] = exception.Message;
            payload["ExceptionDetails"] = exception.ToString();
        }

        logger.Log(level, exception, "{@Log}", payload);
    }
}
