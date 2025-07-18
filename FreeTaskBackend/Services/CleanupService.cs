using FreeTaskBackend.Data;
using FreeTaskBackend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class CleanupService
{
    private readonly AppDbContext _context;
    private readonly EmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CleanupService> _logger;

    public CleanupService(AppDbContext context, EmailService emailService, IConfiguration configuration, ILogger<CleanupService> logger)
    {
        _context = context;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task CleanupUnverifiedAccountsAsync()
    {
        try
        {
            var threshold = DateTime.UtcNow.AddMinutes(-10);
            var unverifiedUsers = await _context.Users
                .Where(u => !u.IsEmailVerified && u.CreatedAt < threshold && u.GoogleId == null)
                .ToListAsync();

            if (unverifiedUsers.Any())
            {
                _context.Users.RemoveRange(unverifiedUsers);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Deleted {Count} unverified accounts", unverifiedUsers.Count);

                // Отправляем уведомление администратору
                var adminEmail = _configuration["AdminEmail"];
                if (!string.IsNullOrEmpty(adminEmail))
                {
                    var subject = "Уведомление: Удаление неподтвержденных аккаунтов";
                    var body = GenerateAdminNotificationEmail(unverifiedUsers.Count);
                    var sent = await _emailService.SendAdminNotificationAsync(adminEmail, subject, body);
                    if (sent)
                    {
                        _logger.LogInformation("Admin notification sent to {AdminEmail}", adminEmail);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send admin notification to {AdminEmail}", adminEmail);
                    }
                }
                else
                {
                    _logger.LogWarning("Admin email not configured in appsettings.json");
                }
            }
            else
            {
                _logger.LogInformation("No unverified accounts to delete");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup of unverified accounts");
        }
    }

    private string GenerateAdminNotificationEmail(int deletedCount)
    {
        var appName = _configuration["EmailSettings:AppName"];
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

        return $@"
        <!DOCTYPE html>
        <html lang=""ru"">
        <head>
            <meta charset=""UTF-8"">
            <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
            <title>{appName} - Уведомление об удалении аккаунтов</title>
        </head>
        <body style=""margin: 0; padding: 0; background-color: #f4f4f4; font-family: Arial, sans-serif;"">
            <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 8px; overflow: hidden;"">
                <tr>
                    <td style=""padding: 30px;"">
                        <h1 style=""font-size: 20px; color: #333333; margin: 0 0 20px;"">Уведомление об удалении неподтвержденных аккаунтов</h1>
                        <p style=""font-size: 16px; color: #666666; margin: 0 0 20px;"">
                            Фоновая задача успешно удалила неподтвержденные аккаунты.
                        </p>
                        <p style=""font-size: 16px; color: #666666; margin: 0 0 10px;"">
                            <strong>Количество удаленных аккаунтов:</strong> {deletedCount}
                        </p>
                        <p style=""font-size: 16px; color: #666666; margin: 0 0 10px;"">
                            <strong>Время выполнения:</strong> {timestamp}
                        </p>
                        <p style=""font-size: 14px; color: #666666; margin: 20px 0 0;"">
                            Это автоматическое уведомление. Пожалуйста, не отвечайте на это письмо.
                        </p>
                    </td>
                </tr>
                <tr>
                    <td style=""text-align: center; padding: 20px; background-color: #f9f9f9;"">
                        <p style=""font-size: 12px; color: #999999; margin: 0;"">
                            © {DateTime.UtcNow.Year} {appName}. Все права защищены.
                        </p>
                    </td>
                </tr>
            </table>
        </body>
        </html>";
    }
}