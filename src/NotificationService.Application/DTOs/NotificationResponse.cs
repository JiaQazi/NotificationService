using NotificationService.Domain.Enums;

namespace NotificationService.Application.DTOs;

public class NotificationResponse
{
    public Guid Id { get; set; }
    public string RecipientId { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RetryCount { get; set; }
}
