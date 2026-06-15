using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Application.Interfaces;
using NotificationService.Infrastructure.Settings;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure.Messaging;

public class RabbitMqPublisher : IMessagePublisher, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private bool _disposed;

    public RabbitMqPublisher(IOptions<RabbitMqSettings> options, ILogger<RabbitMqPublisher> logger)
    {
        _settings = options.Value;
        _logger = logger;
        TryConnect();
    }

    public bool IsConnected => _connection?.IsOpen == true && _channel?.IsOpen == true;

    private void TryConnect()
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = _settings.Host,
                Port = _settings.Port,
                UserName = _settings.Username,
                Password = _settings.Password
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable, skipping publish");
        }
    }

    public Task PublishAsync(string queueName, string message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            if (!IsConnected)
                TryConnect();

            if (!IsConnected)
            {
                _logger.LogWarning("RabbitMQ unavailable, skipping publish");
                return Task.CompletedTask;
            }

            _channel!.QueueDeclare(queue: queueName, durable: true, exclusive: false, autoDelete: false);

            var body = Encoding.UTF8.GetBytes(message);
            var properties = _channel!.CreateBasicProperties();
            properties.Persistent = true;

            _channel.BasicPublish(exchange: string.Empty, routingKey: queueName, basicProperties: properties, body: body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RabbitMQ unavailable, skipping publish");
            _channel = null;
            _connection = null;
        }

        return Task.CompletedTask;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
