using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class NotificationService
{
    private readonly AppDbContext _context;

    public NotificationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Notification>> GetNotificationsAsync(Guid userId, int page, int pageSize)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId)
            ?? throw new KeyNotFoundException("Уведомление не найдено");

        if (notification.UserId != userId)
            throw new UnauthorizedAccessException("Доступ запрещен");

        notification.IsRead = true;
        await _context.SaveChangesAsync();
    }

    public async Task SendTestNotificationAsync(TestNotificationDto dto)
    {
        var user = await _context.Users.FindAsync(dto.UserId)
            ?? throw new KeyNotFoundException("Пользователь не найден");

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = dto.UserId,
            Type = "Test",
            Content = dto.Content,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
    }
}