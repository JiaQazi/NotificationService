using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;
using NotificationService.Infrastructure.Settings;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Infrastructure.BackgroundServices;

public class NotificationConsumerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationConsumerService> _logger;
    private readonly RabbitMqSettings _settings;
    private IConnection? _connection;
    private IModel? _channel;

    public NotificationConsumerService(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationConsumerService> logger,
        IOptions<RabbitMqSettings> options)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _settings = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (TryConnect())
                break;

            _logger.LogWarning("RabbitMQ unavailable, retrying consumer connection in 10 seconds");
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private bool TryConnect()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.Host,
                Port = _settings.Port,
                UserName = _settings.Username,
                Password = _settings.Password,
                DispatchConsumersAsync = true
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            _channel.QueueDeclare(queue: _settings.QueueName, durable: true, exclusive: false, autoDelete: false);
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.Received += async (_, ea) => await HandleMessageAsync(ea);

            _channel.BasicConsume(queue: _settings.QueueName, autoAck: false, consumer: consumer);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable, skipping publish");
            return false;
        }
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken _ = default)
    {
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());

        try
        {
            var notification = JsonSerializer.Deserialize<Notification>(body)
                ?? throw new InvalidOperationException("Deserialized notification was null.");

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<INotificationRepository>();

            var existing = await repository.GetByIdAsync(notification.Id);
            existing.Status = NotificationStatus.Delivered;
            await repository.UpdateAsync(existing);

            _channel!.BasicAck(ea.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process notification message. Payload: {Body}", body);
            _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
        base.Dispose();
    }
}
