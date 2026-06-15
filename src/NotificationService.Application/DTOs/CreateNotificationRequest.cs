using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public class CreateNotificationRequest
{
    public string RecipientId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}
