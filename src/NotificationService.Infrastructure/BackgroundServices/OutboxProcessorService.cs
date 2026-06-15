using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationService.Application.Interfaces;
using NotificationService.Infrastructure.Settings;

namespace NotificationService.Infrastructure.BackgroundServices;

public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorService> _logger;
    private readonly string _queueName;

    public OutboxProcessorService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxProcessorService> logger,
        IOptions<RabbitMqSettings> rabbitMqOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _queueName = rabbitMqOptions.Value.QueueName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var outboxRepository = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();
        var publisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();

        var messages = await outboxRepository.GetUnprocessedAsync(10);

        foreach (var message in messages)
        {
            if (stoppingToken.IsCancellationRequested) break;

            if (message.RetryCount >= 3)
                continue;

            try
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, message.RetryCount));
                if (message.RetryCount > 0)
                    await Task.Delay(delay, stoppingToken);

                await publisher.PublishAsync(_queueName, message.Payload);
                await outboxRepository.MarkAsProcessedAsync(message.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish outbox message {MessageId}. RetryCount: {RetryCount}", message.Id, message.RetryCount);
                await outboxRepository.IncrementRetryAsync(message.Id);
            }
        }
    }
}
