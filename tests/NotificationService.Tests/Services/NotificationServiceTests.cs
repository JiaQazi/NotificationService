using System.Text.Json;
using Moq;
using NotificationService.Application.DTOs;
using NotificationService.Application.Interfaces;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Tests.Services;

public class NotificationServiceTests
{
    private readonly Mock<INotificationRepository> _notificationRepositoryMock;
    private readonly Mock<IOutboxRepository> _outboxRepositoryMock;
    private readonly Application.Services.NotificationService _sut;

    public NotificationServiceTests()
    {
        _notificationRepositoryMock = new Mock<INotificationRepository>();
        _outboxRepositoryMock = new Mock<IOutboxRepository>();
        _sut = new Application.Services.NotificationService(
            _notificationRepositoryMock.Object,
            _outboxRepositoryMock.Object);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateNotificationWithPendingStatus()
    {
        var request = BuildRequest();

        var response = await _sut.CreateAsync(request);

        Assert.Equal(NotificationStatus.Pending, response.Status);
    }

    [Fact]
    public async Task CreateAsync_ShouldMapRequestFieldsToNotification()
    {
        var request = BuildRequest();

        var response = await _sut.CreateAsync(request);

        Assert.Equal(request.RecipientId, response.RecipientId);
        Assert.Equal(request.Type, response.Type);
        Assert.Equal(request.Subject, response.Subject);
        Assert.Equal(request.Body, response.Body);
        Assert.NotEqual(Guid.Empty, response.Id);
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateOutboxMessageWithSerializedNotificationPayload()
    {
        var request = BuildRequest();
        OutboxMessage? capturedOutbox = null;

        _outboxRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<OutboxMessage>()))
            .Callback<OutboxMessage>(msg => capturedOutbox = msg)
            .Returns(Task.CompletedTask);

        var response = await _sut.CreateAsync(request);

        Assert.NotNull(capturedOutbox);
        Assert.Equal(nameof(Notification), capturedOutbox.EventType);
        Assert.False(string.IsNullOrWhiteSpace(capturedOutbox.Payload));

        var deserialized = JsonSerializer.Deserialize<Notification>(capturedOutbox.Payload);
        Assert.NotNull(deserialized);
        Assert.Equal(response.Id, deserialized.Id);
        Assert.Equal(request.RecipientId, deserialized.RecipientId);
    }

    [Fact]
    public async Task CreateAsync_ShouldCallNotificationRepositoryAddAsync()
    {
        var request = BuildRequest();

        await _sut.CreateAsync(request);

        _notificationRepositoryMock.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldCallOutboxRepositoryAddAsync()
    {
        var request = BuildRequest();

        await _sut.CreateAsync(request);

        _outboxRepositoryMock.Verify(r => r.AddAsync(It.IsAny<OutboxMessage>()), Times.Once);
    }

    private static CreateNotificationRequest BuildRequest() => new()
    {
        RecipientId = "user-123",
        Type = NotificationType.Email,
        Subject = "Welcome",
        Body = "Hello, welcome to the service!"
    };
}
