using System.Text;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;
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
    IRetryPolicyProvider retryPolicies)
    : BackgroundService
{
    private IConnection? _connection;
    private IModel? _channel;
    private CancellationToken _stoppingToken;

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
        var body = Encoding.UTF8.GetString(args.Body.ToArray());

        try
        {
            logger.LogInformation(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Mensagem recebida.",
                message: $"delivery={args.DeliveryTag}");

            using var scope = scopeFactory.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<DtfDailyCaptureWorkflow>();

            await workflow.ProcessAsync(_stoppingToken);

            _channel.BasicAck(args.DeliveryTag, multiple: false);
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
            using var scope = scopeFactory.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<DtfDailyCaptureWorkflow>();
            workflow.NotifyCritical(ex);
            _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
        }
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
