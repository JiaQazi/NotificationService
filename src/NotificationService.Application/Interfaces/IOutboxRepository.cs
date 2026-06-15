using NotificationService.Domain.Entities;

namespace NotificationService.Application.Interfaces;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message);
    Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize);
    Task MarkAsProcessedAsync(Guid id);
    Task IncrementRetryAsync(Guid id);
}
