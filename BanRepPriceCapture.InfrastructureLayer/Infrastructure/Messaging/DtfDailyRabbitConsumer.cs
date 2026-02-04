using System.Text;
using BanRepPriceCapture.ApplicationLayer.Workflows;
using BanRepPriceCapture.InfrastructureLayer.Configuration;
using BanRepPriceCapture.ApplicationLayer.Flow;
using BanRepPriceCapture.ApplicationLayer.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BanRepPriceCapture.InfrastructureLayer.Messaging;

public sealed class DtfDailyRabbitConsumer(
    IStructuredLogger logger,
    IServiceScopeFactory scopeFactory,
    DtfDailyCaptureSettings settings,
    IConnectionFactory connectionFactory,
    IFlowContextAccessor flowContext)
    : BackgroundService
{
    private IConnection? _connection;
    private IModel? _channel;
    private CancellationToken _stoppingToken;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        if (string.IsNullOrWhiteSpace(settings.QueueName))
        {
            throw new InvalidOperationException("QueueName nao configurado.");
        }

        _connection = connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();
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

        return Task.CompletedTask;
    }

    private async Task HandleMessageAsync(object sender, BasicDeliverEventArgs args)
    {
        if (_channel is null)
        {
            return;
        }

        var messageId = args.BasicProperties?.MessageId;
        var flowId = TryGetFlowId(messageId) ?? Guid.NewGuid();
        flowContext.SetFlowId(flowId);
        var body = Encoding.UTF8.GetString(args.Body.ToArray());

        try
        {
            logger.LogInformation(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Mensagem recebida.",
                message: $"delivery={args.DeliveryTag} flow_id={flowId}");

            using var scope = scopeFactory.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<DtfDailyCaptureWorkflow>();

            await workflow.ProcessAsync(_stoppingToken);

            _channel.BasicAck(args.DeliveryTag, multiple: false);
            logger.LogInformation(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Mensagem processada com sucesso.",
                message: $"flow_id={flowId}");
        }
        catch (Exception ex)
        {
            logger.LogCritical(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Falha ao processar mensagem.",
                message: $"flow_id={flowId} payload={body}",
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

    private static Guid? TryGetFlowId(string? messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return null;
        }

        return Guid.TryParse(messageId, out var parsed) ? parsed : null;
    }
}
