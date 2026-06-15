using System.Data;
using Dapper;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;

namespace NotificationService.Infrastructure.Repositories;

public class OutboxRepository : IOutboxRepository
{
    private readonly IDbConnection _dbConnection;

    public OutboxRepository(IDbConnection dbConnection)
    {
        _dbConnection = dbConnection;
    }

    public async Task AddAsync(OutboxMessage message)
    {
        const string sql = """
            INSERT INTO OutboxMessages (Id, EventType, Payload, CreatedAt, ProcessedAt, RetryCount)
            VALUES (@Id, @EventType, @Payload, @CreatedAt, @ProcessedAt, @RetryCount)
            """;

        await _dbConnection.ExecuteAsync(sql, message);
    }

    public async Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize)
    {
        const string sql = """
            SELECT TOP (@BatchSize) Id, EventType, Payload, CreatedAt, ProcessedAt, RetryCount
            FROM OutboxMessages
            WHERE ProcessedAt IS NULL AND RetryCount < 3
            ORDER BY CreatedAt ASC
            """;

        return await _dbConnection.QueryAsync<OutboxMessage>(sql, new { BatchSize = batchSize });
    }

    public async Task MarkAsProcessedAsync(Guid id)
    {
        const string sql = """
            UPDATE OutboxMessages SET ProcessedAt = @ProcessedAt WHERE Id = @Id
            """;

        await _dbConnection.ExecuteAsync(sql, new { ProcessedAt = DateTime.UtcNow, Id = id });
    }

    public async Task IncrementRetryAsync(Guid id)
    {
        const string sql = """
            UPDATE OutboxMessages SET RetryCount = RetryCount + 1 WHERE Id = @Id
            """;

        await _dbConnection.ExecuteAsync(sql, new { Id = id });
    }
}
