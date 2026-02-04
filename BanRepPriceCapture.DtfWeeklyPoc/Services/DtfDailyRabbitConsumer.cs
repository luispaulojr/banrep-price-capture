using System.Text;
using BanRepPriceCapture.DtfWeeklyPoc.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace BanRepPriceCapture.DtfWeeklyPoc.Services;

public sealed class DtfDailyRabbitConsumer : BackgroundService
{
    private readonly ILogger<DtfDailyRabbitConsumer> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DtfDailyCaptureSettings _settings;
    private readonly IConnectionFactory _connectionFactory;

    private IConnection? _connection;
    private IModel? _channel;
    private CancellationToken _stoppingToken;

    public DtfDailyRabbitConsumer(
        ILogger<DtfDailyRabbitConsumer> logger,
        IServiceScopeFactory scopeFactory,
        DtfDailyCaptureSettings settings,
        IConnectionFactory connectionFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _settings = settings;
        _connectionFactory = connectionFactory;
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

        _logger.LogInformation("Consumidor RabbitMQ iniciado. fila={Queue}", _settings.QueueName);

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
        var body = Encoding.UTF8.GetString(args.Body.ToArray());

        try
        {
            _logger.LogInformation("Mensagem recebida. delivery={DeliveryTag} flow_id={FlowId}", args.DeliveryTag, flowId);

            using var scope = _scopeFactory.CreateScope();
            var workflow = scope.ServiceProvider.GetRequiredService<DtfDailyCaptureWorkflow>();

            await workflow.ProcessAsync(flowId, _stoppingToken);

            _channel.BasicAck(args.DeliveryTag, multiple: false);
            _logger.LogInformation("Mensagem processada com sucesso. flow_id={FlowId}", flowId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar mensagem. flow_id={FlowId} payload={Payload}", flowId, body);
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
