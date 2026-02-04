using System.Text.Json;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;
using Microsoft.Extensions.Logging;

namespace BanRepPriceCapture.InfrastructureLayer.Logging;

public sealed class StructuredLogger(
    ILogger<StructuredLogger> logger,
    IFlowContextAccessor flowContext) : IStructuredLogger
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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
        var payload = new StructuredLogEntry(
            Level: level.ToString(),
            FlowId: flowContext.FlowId,
            Method: method,
            Description: description,
            Message: string.IsNullOrWhiteSpace(message) ? null : message,
            Exception: exception?.Message,
            ExceptionDetails: exception?.ToString());

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        logger.Log(level, exception, "{LogJson}", json);
    }

    private sealed record StructuredLogEntry(
        string Level,
        Guid FlowId,
        string Method,
        string Description,
        string? Message,
        string? Exception,
        string? ExceptionDetails);
}
