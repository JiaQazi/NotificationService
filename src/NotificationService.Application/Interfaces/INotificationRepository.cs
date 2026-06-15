using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

public interface INotificationRepository
{
    Task<Notification> GetByIdAsync(Guid id);
    Task<IEnumerable<Notification>> GetAllAsync();
    Task AddAsync(Notification notification);
    Task UpdateAsync(Notification notification);
}
