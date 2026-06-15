using Dapper;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Infrastructure.Persistence;
using System.Data;

namespace NotificationService.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly AppDbContext _context;
    private readonly IDbConnection _dbConnection;

    public NotificationRepository(AppDbContext context, IDbConnection dbConnection)
    {
        _context = context;
        _dbConnection = dbConnection;
    }

    public async Task<Notification> GetByIdAsync(Guid id)
    {
        return await _context.Notifications.FindAsync(id)
            ?? throw new KeyNotFoundException($"Notification {id} not found.");
    }

    public async Task<IEnumerable<Notification>> GetAllAsync()
    {
        const string sql = """
            SELECT Id, RecipientId, Type, Subject, Body, Status, CreatedAt, RetryCount
            FROM Notifications
            """;

        return await _dbConnection.QueryAsync<Notification>(sql);
    }

    public async Task AddAsync(Notification notification)
    {
        await _context.Notifications.AddAsync(notification);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Notification notification)
    {
        _context.Notifications.Update(notification);
        await _context.SaveChangesAsync();
    }
}
