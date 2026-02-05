using System.Collections.Concurrent;
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
    private readonly ConcurrentDictionary<Guid, int> _requeueAttempts = new();
    private readonly ConcurrentDictionary<Guid, bool> _partialRetryNotified = new();
    private readonly ConcurrentDictionary<Guid, bool> _failureNotified = new();
    private readonly ConcurrentDictionary<Guid, bool> _requeueThresholdNotified = new();

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
                NotifyFailureOnce(flowId, ex);
                NotifyPartialRetry(flowId);
                NotifyRequeueThreshold(flowId);
            }
            catch (Exception notifyEx)
            {
                logger.LogError(
                    method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                    description: "Falha ao notificar erro crítico.",
                    exception: notifyEx);
            }

            _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
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

    private void NotifyPartialRetry(Guid flowId)
    {
        var attempt = _requeueAttempts.AddOrUpdate(flowId, 1, (_, current) => current + 1);
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

    private void NotifyRequeueThreshold(Guid flowId)
    {
        var attempt = _requeueAttempts.GetOrAdd(flowId, 1);
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

    private void ClearNotificationState(Guid flowId)
    {
        _requeueAttempts.TryRemove(flowId, out _);
        _partialRetryNotified.TryRemove(flowId, out _);
        _failureNotified.TryRemove(flowId, out _);
        _requeueThresholdNotified.TryRemove(flowId, out _);
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
