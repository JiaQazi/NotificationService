namespace NotificationService.Application.Interfaces;

public interface IMessagePublisher
{
    Task PublishAsync(string queueName, string message);
}
