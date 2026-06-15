using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotificationService.Application.Interfaces;
using NotificationService.Infrastructure.Messaging;

namespace NotificationService.Infrastructure.HealthChecks;

public class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqPublisher _publisher;

    public RabbitMqHealthCheck(IMessagePublisher publisher)
    {
        _publisher = (RabbitMqPublisher)publisher;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = _publisher.IsConnected
            ? HealthCheckResult.Healthy("RabbitMQ is connected")
            : HealthCheckResult.Degraded("RabbitMQ is unavailable");

        return Task.FromResult(result);
    }
}
