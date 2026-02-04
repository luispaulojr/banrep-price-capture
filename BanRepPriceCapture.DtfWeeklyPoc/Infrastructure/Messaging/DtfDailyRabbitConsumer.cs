using System.Text;
using BanRepPriceCapture.DtfWeeklyPoc.Application.Workflows;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Configuration;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Flow;
using BanRepPriceCapture.DtfWeeklyPoc.Shared.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BanRepPriceCapture.DtfWeeklyPoc.Infrastructure.Messaging;

public sealed class DtfDailyRabbitConsumer : BackgroundService
{
    private readonly IStructuredLogger _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DtfDailyCaptureSettings _settings;
    private readonly IConnectionFactory _connectionFactory;
    private readonly IFlowContextAccessor _flowContext;

    private IConnection? _connection;
    private IModel? _channel;
    private CancellationToken _stoppingToken;

    public DtfDailyRabbitConsumer(
        IStructuredLogger logger,
        IServiceScopeFactory scopeFactory,
        DtfDailyCaptureSettings settings,
        IConnectionFactory connectionFactory,
        IFlowContextAccessor flowContext)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _settings = settings;
        _connectionFactory = connectionFactory;
        _flowContext = flowContext;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        if (string.IsNullOrWhiteSpace(_settings.QueueName))
        {
            throw new InvalidOperationException("QueueName nao configurado.");
        }

        _connection = _connectionFactory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += HandleMessageAsync;

        _channel.BasicConsume(
            queue: _settings.QueueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation(
            method: "DtfDailyRabbitConsumer.ExecuteAsync",
            description: "Consumidor RabbitMQ iniciado.",
            message: $"fila={_settings.QueueName}");

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
        _flowContext.SetFlowId(flowId);
        var body = Encoding.UTF8.GetString(args.Body.ToArray());

        try
        {
            _logger.LogInformation(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Mensagem recebida.",
                message: $"delivery={args.DeliveryTag} flow_id={flowId}");

            using var scope = _scopeFactory.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<DtfDailyCaptureWorkflow>();

            await workflow.ProcessAsync(_stoppingToken);

            _channel.BasicAck(args.DeliveryTag, multiple: false);
            _logger.LogInformation(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Mensagem processada com sucesso.",
                message: $"flow_id={flowId}");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                method: "DtfDailyRabbitConsumer.HandleMessageAsync",
                description: "Falha ao processar mensagem.",
                message: $"flow_id={flowId} payload={body}",
                exception: ex);
            using var scope = _scopeFactory.CreateScope();
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
