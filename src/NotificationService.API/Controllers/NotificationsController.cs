using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.DTOs;

namespace NotificationService.API.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService.Application.Services.NotificationService _notificationService;
    private readonly Application.Interfaces.INotificationRepository _notificationRepository;

    public NotificationsController(
        NotificationService.Application.Services.NotificationService notificationService,
        Application.Interfaces.INotificationRepository notificationRepository)
    {
        _notificationService = notificationService;
        _notificationRepository = notificationRepository;
    }

    [HttpPost]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateNotificationRequest request)
    {
        var response = await _notificationService.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(NotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var notification = await _notificationRepository.GetByIdAsync(id);
            return Ok(MapToResponse(notification));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<NotificationResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll()
    {
        var notifications = await _notificationRepository.GetAllAsync();
        return Ok(notifications.Select(MapToResponse));
    }

    private static NotificationResponse MapToResponse(Domain.Entities.Notification n) => new()
    {
        Id = n.Id,
        RecipientId = n.RecipientId,
        Type = n.Type,
        Subject = n.Subject,
        Body = n.Body,
        Status = n.Status,
        CreatedAt = n.CreatedAt,
        RetryCount = n.RetryCount
    };
}
