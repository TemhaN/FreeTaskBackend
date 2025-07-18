using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly AppDbContext _context;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(NotificationService notificationService, AppDbContext context, ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _context = context;
        _logger = logger;
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var notifications = await _notificationService.GetNotificationsAsync(userId, page, pageSize);
            var response = notifications.Select(n => new NotificationResponseDto
            {
                Id = n.Id,
                UserId = n.UserId,
                Type = n.Type,
                Content = n.Content,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt
            }).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving notifications for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при получении уведомлений" });
        }
    }

    [Authorize]
    [HttpPut("{id}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == id);
            if (notification == null || notification.UserId != userId)
                return NotFound("Notification not found or access denied.");

            await _notificationService.MarkAsReadAsync(id, userId);
            return Ok(new { message = "Уведомление помечено как прочитанное" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read: {NotificationId}", id);
            return BadRequest(new { message = "Ошибка при отметке уведомления" });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("test")]
    public async Task<IActionResult> SendTestNotification([FromBody] TestNotificationDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            await _notificationService.SendTestNotificationAsync(dto);
            return Ok(new { message = "Тестовое уведомление отправлено" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test notification");
            return BadRequest(new { message = "Ошибка при отправке тестового уведомления" });
        }
    }
}