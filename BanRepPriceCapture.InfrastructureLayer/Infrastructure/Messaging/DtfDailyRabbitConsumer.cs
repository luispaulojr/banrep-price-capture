using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;
using BanRepPriceCapture.ApplicationLayer.Application.Notifications;
using BanRepPriceCapture.ApplicationLayer.Application.Workflows;
using BanRepPriceCapture.InfrastructureLayer.Configuration;
using BanRepPriceCapture.InfrastructureLayer.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BanRepPriceCapture.InfrastructureLayer.Infrastructure.Messaging;

public sealed class DtfDailyRabbitConsumer(
    IStructuredLogger logger,
    IServiceScopeFactory scopeFactory,
    DtfDailyCaptureSettings settings,
    IConnectionFactory connectionFactory,
    IFlowContextAccessor flowContext,
    IFlowIdProvider flowIdProvider,
    INotificationService notificationService,
    IRetryPolicyProvider retryPolicies)
    : BackgroundService
{
    private IConnection? _connection;
    private IModel? _channel;
    private CancellationToken _stoppingToken;
    private readonly ConcurrentDictionary<Guid, bool> _partialRetryNotified = new();
    private readonly ConcurrentDictionary<Guid, bool> _failureNotified = new();
    private readonly ConcurrentDictionary<Guid, bool> _requeueThresholdNotified = new();
    private readonly ConcurrentDictionary<Guid, bool> _retryLimitNotified = new();
    private const string RetryCountHeader = "x-retry-count";
    private const string DeliveryCountHeader = "x-delivery-count";
    private const string FlowIdHeader = "FlowId";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        if (string.IsNullOrWhiteSpace(settings.QueueName))
        {
            throw new InvalidOperationException("QueueName nao configurado.");
        }

        _connection = await retryPolicies.ExecuteAsync(
            _ => Task.FromResult(connectionFactory.CreateConnection()),
            RetryPolicyKind.RabbitMqConnection,
            "DtfDailyRabbitConsumer.ExecuteAsync",
            stoppingToken);

        _channel = await retryPolicies.ExecuteAsync(
            _ => Task.FromResult(_connection.CreateModel()),
            RetryPolicyKind.RabbitMqConnection,
            "DtfDailyRabbitConsumer.ExecuteAsync",
            stoppingToken);
        _channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleMessageAsync;

        _channel.BasicConsume(
            queue: settings.QueueName,
            autoAck: false,
            consumer: consumer);

        logger.LogInformation(
            method: "DtfDailyRabbitConsumer.ExecuteAsync",
            description: "Consumidor RabbitMQ iniciado.",
            message: $"fila={settings.QueueName}");

        return;
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        if (_channel is null)
        {
            return;
        }

        var messageId = args.BasicProperties?.MessageId;
        var flowId = flowIdProvider.CreateFromMessageId(messageId);
        flowContext.SetFlowId(flowId);
        flowContext.SetCaptureDate(DateOnly.FromDateTime(DateTime.UtcNow));
        var body = Encoding.UTF8.GetString(args.Body.ToArray());

        using var scope = scopeFactory.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<DtfDailyCaptureWorkflow>();

        try
        {
            logger.LogInformation(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Mensagem recebida.",
                message: $"delivery={args.DeliveryTag}");

            await workflow.ProcessAsync(_stoppingToken);

            _channel.BasicAck(args.DeliveryTag, multiple: false);
            ClearNotificationState(flowId);

            logger.LogInformation(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Mensagem processada com sucesso.",
                message: "processamento=ok");
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Falha ao processar mensagem.",
                message: $"payload={body}",
                exception: ex);

            try
            {
                var currentRetryCount = GetRetryCount(args);
                var nextRetryCount = currentRetryCount + 1;

                NotifyFailureOnce(flowId, ex);
                NotifyPartialRetry(flowId, nextRetryCount);
                NotifyRequeueThreshold(flowId, nextRetryCount);

                if (nextRetryCount > settings.MaxRetryAttempts)
                {
                    HandleRetryLimitExceeded(args, flowId, nextRetryCount);
                }
                else
                {
                    RepublishForRetry(args, flowId, nextRetryCount);
                }
            }
            catch (Exception notifyEx)
            {
                logger.LogError(
                    method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                    description: "Falha ao notificar erro crítico.",
                    exception: notifyEx);
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
                return;
            }

            _channel.BasicAck(args.DeliveryTag, multiple: false);
        }
    }

    private void NotifyFailureOnce(Guid flowId, Exception ex)
    {
        if (!_failureNotified.TryAdd(flowId, true))
        {
            return;
        }

        notificationService.NotifyError(new NotificationPayload
        {
            Title = "Falha na execucao DTF Daily",
            Description = ex.Message,
            Feature = "DTF Daily Capture",
            Source = "BanRepPriceCapture",
            CorrelationId = flowId.ToString(),
            TemplateName = "dtf-daily-execution-failed"
        }, ex);
    }

    private void NotifyPartialRetry(Guid flowId, int attempt)
    {
        if (attempt > 1 || !_partialRetryNotified.TryAdd(flowId, true))
        {
            return;
        }

        notificationService.NotifyWarn(new NotificationPayload
        {
            Title = "Reprocessamento parcial DTF Daily",
            Description = $"Reprocessamento parcial detectado. Tentativa={attempt}.",
            Feature = "DTF Daily Capture",
            Source = "BanRepPriceCapture",
            CorrelationId = flowId.ToString(),
            TemplateName = "dtf-daily-partial-retry"
        });
    }

    private void NotifyRequeueThreshold(Guid flowId, int attempt)
    {
        if (attempt < settings.RequeueNotificationThreshold)
        {
            return;
        }

        if (!_requeueThresholdNotified.TryAdd(flowId, true))
        {
            return;
        }

        notificationService.NotifyError(new NotificationPayload
        {
            Title = "Requeue excedido DTF Daily",
            Description = $"Tentativas de reprocessamento excederam o limite configurado. Tentativa={attempt}.",
            Feature = "DTF Daily Capture",
            Source = "BanRepPriceCapture",
            CorrelationId = flowId.ToString(),
            TemplateName = "dtf-daily-requeue-threshold"
        });
    }

    private int GetRetryCount(BasicDeliverEventArgs args)
    {
        var headers = args.BasicProperties?.Headers;
        var headerRetry = TryGetHeaderInt(headers, RetryCountHeader);
        if (headerRetry is not null)
        {
            return Math.Max(0, headerRetry.Value);
        }

        var deliveryCount = TryGetHeaderInt(headers, DeliveryCountHeader);
        if (deliveryCount is not null)
        {
            return Math.Max(0, deliveryCount.Value - 1);
        }

        return 0;
    }

    private static int? TryGetHeaderInt(IDictionary<string, object>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return value switch
        {
            byte[] bytes => int.TryParse(Encoding.UTF8.GetString(bytes), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null,
            string text => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null,
            sbyte or byte or short or ushort or int or uint or long or ulong => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            _ => null
        };
    }

    private void RepublishForRetry(BasicDeliverEventArgs args, Guid flowId, int retryCount)
    {
        if (_channel is null)
        {
            return;
        }

        var properties = _channel.CreateBasicProperties();
        var source = args.BasicProperties;
        if (source is not null)
        {
            properties.AppId = source.AppId;
            properties.ClusterId = source.ClusterId;
            properties.ContentEncoding = source.ContentEncoding;
            properties.ContentType = source.ContentType;
            properties.CorrelationId = source.CorrelationId;
            properties.DeliveryMode = source.DeliveryMode;
            properties.Expiration = source.Expiration;
            properties.MessageId = source.MessageId;
            properties.Priority = source.Priority;
            properties.ReplyTo = source.ReplyTo;
            properties.Type = source.Type;
            properties.UserId = source.UserId;
            properties.Timestamp = source.Timestamp;
            properties.Headers = source.Headers is null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(source.Headers);
        }
        else
        {
            properties.Headers = new Dictionary<string, object>();
        }

        properties.MessageId ??= flowId.ToString();
        properties.Headers[RetryCountHeader] = retryCount;
        properties.Headers[FlowIdHeader] = flowId.ToString();

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: settings.QueueName,
            basicProperties: properties,
            body: args.Body);
    }

    private void HandleRetryLimitExceeded(BasicDeliverEventArgs args, Guid flowId, int retryCount)
    {
        if (_channel is null)
        {
            return;
        }

        try
        {
            NotifyRetryLimitExceeded(flowId, retryCount);
        }
        catch (Exception notifyEx)
        {
            logger.LogError(
                method: "DtfDailyRabbitConsumer.HandleRetryLimitExceeded",
                description: "Falha ao notificar limite de requeue.",
                exception: notifyEx);
        }

        if (!string.IsNullOrWhiteSpace(settings.DeadLetterQueueName))
        {
            var properties = _channel.CreateBasicProperties();
            var source = args.BasicProperties;
            if (source is not null)
            {
                properties.AppId = source.AppId;
                properties.ClusterId = source.ClusterId;
                properties.ContentEncoding = source.ContentEncoding;
                properties.ContentType = source.ContentType;
                properties.CorrelationId = source.CorrelationId;
                properties.DeliveryMode = source.DeliveryMode;
                properties.Expiration = source.Expiration;
                properties.MessageId = source.MessageId;
                properties.Priority = source.Priority;
                properties.ReplyTo = source.ReplyTo;
                properties.Type = source.Type;
                properties.UserId = source.UserId;
                properties.Timestamp = source.Timestamp;
                properties.Headers = source.Headers is null
                    ? new Dictionary<string, object>()
                    : new Dictionary<string, object>(source.Headers);
            }
            else
            {
                properties.Headers = new Dictionary<string, object>();
            }

            properties.MessageId ??= flowId.ToString();
            properties.Headers[RetryCountHeader] = retryCount;
            properties.Headers[FlowIdHeader] = flowId.ToString();

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: settings.DeadLetterQueueName,
                basicProperties: properties,
                body: args.Body);
        }
    }

    private void NotifyRetryLimitExceeded(Guid flowId, int attempt)
    {
        if (!_retryLimitNotified.TryAdd(flowId, true))
        {
            return;
        }

        notificationService.NotifyError(new NotificationPayload
        {
            Title = "Limite de reprocessamento excedido DTF Daily",
            Description = $"Tentativas de reprocessamento excederam o limite configurado. Tentativa={attempt}.",
            Feature = "DTF Daily Capture",
            Source = "BanRepPriceCapture",
            CorrelationId = flowId.ToString(),
            TemplateName = "dtf-daily-retry-limit-exceeded"
        });
    }

    private void ClearNotificationState(Guid flowId)
    {
        _partialRetryNotified.TryRemove(flowId, out _);
        _failureNotified.TryRemove(flowId, out _);
        _requeueThresholdNotified.TryRemove(flowId, out _);
        _retryLimitNotified.TryRemove(flowId, out _);
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _channel?.Close();
        _connection?.Close();
        return base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }

}
