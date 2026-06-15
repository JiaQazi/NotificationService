using System.Text.Json;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Application.Services;

public class NotificationService
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IOutboxRepository _outboxRepository;

    public NotificationService(INotificationRepository notificationRepository, IOutboxRepository outboxRepository)
    {
        _notificationRepository = notificationRepository;
        _outboxRepository = outboxRepository;
    }

    public async Task<NotificationResponse> CreateAsync(CreateNotificationRequest request)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            RecipientId = request.RecipientId,
            Type = request.Type,
            Subject = request.Subject,
            Body = request.Body,
            Status = NotificationStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = nameof(Notification),
            Payload = JsonSerializer.Serialize(notification),
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        await _notificationRepository.AddAsync(notification);
        await _outboxRepository.AddAsync(outboxMessage);

        return new NotificationResponse
        {
            Id = notification.Id,
            RecipientId = notification.RecipientId,
            Type = notification.Type,
            Subject = notification.Subject,
            Body = notification.Body,
            Status = notification.Status,
            CreatedAt = notification.CreatedAt,
            RetryCount = notification.RetryCount
        };
    }
}
