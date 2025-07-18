using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace FreeTaskBackend.Services;

public class EmailService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;
    private const int MaxVerificationAttempts = 3; // Максимум 3 попытки в день

    public EmailService(AppDbContext context, IConfiguration configuration, ILogger<EmailService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> SendVerificationCodeAsync(string email, string type)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            _logger.LogWarning("User not found for email: {Email}", email);
            return false; // Изменено с true на false
        }

        // Проверяем лимит попыток
        if (!await CanSendVerificationCode(user))
        {
            _logger.LogWarning("Cannot send verification code to {Email}: limit exceeded or too frequent", email);
            return false;
        }

        var oldCodes = await _context.VerificationCodes
            .Where(vc => vc.UserId == user.Id && vc.Type == type && !vc.IsUsed)
            .ToListAsync();
        foreach (var oldCode in oldCodes)
        {
            oldCode.IsUsed = true;
        }

        // Генерируем 6-значный код
        var code = new Random().Next(100000, 999999).ToString();
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        // Сохраняем код в базе
        var verificationCode = new VerificationCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Code = code,
            Type = type,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsUsed = false
        };

        _context.VerificationCodes.Add(verificationCode);

        // Увеличиваем счетчик попыток
        user.VerificationAttempts++;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // Формируем письмо
        var subject = type == "EmailVerification" ? "Подтверждение email" : "Сброс пароля";
        var body = GenerateEmailTemplate(email, code, type);

        var sent = await SendEmailAsync(email, subject, body);
        if (!sent)
        {
            _logger.LogWarning("Failed to send verification code to {Email} for {Type}, attempts: {Attempts}", email, type, user.VerificationAttempts);
            return false;
        }

        _logger.LogInformation("Verification code sent to {Email} for {Type}, attempts: {Attempts}", email, type, user.VerificationAttempts);
        return true;
    }

    private async Task<bool> SendEmailAsync(string toEmail, string subject, string body)
    {
        try
        {
            var smtpConfig = _configuration.GetSection("Smtp");
            _logger.LogInformation("SMTP Config: Host={Host}, Port={Port}, FromEmail={FromEmail}, Username={Username}",
                smtpConfig["Host"], smtpConfig["Port"], smtpConfig["FromEmail"], smtpConfig["Username"]);

            var client = new SmtpClient(smtpConfig["Host"], int.Parse(smtpConfig["Port"]))
            {
                Credentials = new System.Net.NetworkCredential(smtpConfig["Username"], smtpConfig["Password"]),
                EnableSsl = true
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(smtpConfig["FromEmail"], smtpConfig["FromName"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mailMessage.To.Add(toEmail);

            await client.SendMailAsync(mailMessage);
            _logger.LogInformation("Email successfully sent to {ToEmail} with subject: {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToEmail} with subject: {Subject}. Error: {Message}", toEmail, subject, ex.Message);
            return false;
        }
    }
    public async Task<bool> SendAdminNotificationAsync(string toEmail, string subject, string body)
    {
        return await SendEmailAsync(toEmail, subject, body);
    }
    private async Task<bool> CanSendVerificationCode(User user)
    {
        var now = DateTime.UtcNow;
        if (user.LastAttemptReset == null || user.LastAttemptReset.Value.Date < now.Date)
        {
            user.VerificationAttempts = 0;
            user.LastAttemptReset = now;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Verification attempts reset for user: {Email}", user.Email);
        }

        if (user.VerificationAttempts > 0 && user.LastAttemptReset != null && (now - user.LastAttemptReset.Value).TotalMinutes < 1)
        {
            _logger.LogWarning("Too frequent verification code requests for {Email}", user.Email);
            return false;
        }

        if (user.VerificationAttempts >= MaxVerificationAttempts)
        {
            _logger.LogWarning("Verification attempt limit exceeded for {Email}", user.Email);
            return false;
        }

        return true;
    }

    private string GenerateEmailTemplate(string email, string code, string type)
    {
        var appName = _configuration["EmailSettings:AppName"];
        var logoUrl = _configuration["EmailSettings:LogoUrl"];
        var primaryColor = _configuration["EmailSettings:PrimaryColor"];
        var supportUrl = _configuration["EmailSettings:SupportUrl"];
        var frontendUrl = _configuration["EmailSettings:FrontendUrl"] ?? "https://yourdomain.com";
        var actionText = type == "EmailVerification" ? "подтверждения email" : "сброса пароля";
        var actionUrl = type == "EmailVerification"
            ? $"{frontendUrl}/api/v1/email/verify-link?email={Uri.EscapeDataString(email)}&code={code}"
            : $"{frontendUrl}/reset-password?code={code}&email={Uri.EscapeDataString(email)}";

        return $@"
    <!DOCTYPE html>
    <html lang=""ru"">
    <head>
        <meta charset=""UTF-8"">
        <meta name=""viewport"" content=""width=device-width; initial-scale=1.0"">
        <title>{appName} - {actionText}</title>
    </head>
    <body style=""margin: 0; padding: 0; background-color: #f4f4f4; font-family: Arial, sans-serif;"">
        <table role=""presentation"" width=""100%"" cellspacing=""0"" cellpadding=""0"" style=""max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 8px; overflow: hidden;"">
            //<tr>
            //    <td style=""text-align: center; padding: 20px; background-color: {primaryColor};"">
            //        <img src=""{logoUrl}"" alt=""{appName} Logo"" style=""max-width: 150px; height: auto;"">
            //    </td>
            //</tr>
            <tr>
                <td style=""padding: 30px; text-align: center;"">
                    <h1 style=""font-size: 24px; color: #333333; margin: 0 0 20px;"">Ваш код для {actionText}</h1>
                    <p style=""font-size: 16px; color: #666666; margin: 0 0 20px;"">Нажмите кнопку ниже, чтобы завершить процесс:</p>
                    <a href=""{actionUrl}"" style=""display: inline-block; padding: 15px 30px; font-size: 16px; color: #ffffff; background-color: {primaryColor}; border-radius: 5px; text-decoration: none; font-weight: bold;"">
                        Подтвердить {actionText}
                    </a>
                    <p style=""font-size: 16px; color: #666666; margin: 20px 0;"">Или используйте этот код:</p>
                    <div style=""display: inline-block; padding: 15px 25px; font-size: 24px; font-weight: bold; color: #ffffff; background-color: {primaryColor}; border-radius: 5px; letter-spacing: 2px;"">
                        {code}
                    </div>
                    <p style=""font-size: 14px; color: #666666; margin: 20px 0;"">Код действителен в течение 10 минут.</p>
                    <p style=""font-size: 14px; color: #666666; margin: 0;"">Если вы не запрашивали этот код, проигнорируйте это письмо.</p>
                </td>
            </tr>
            <tr>
                <td style=""text-align: center; padding: 20px; background-color: #f9f9f9;"">
                    <p style=""font-size: 12px; color: #999999; margin: 0;"">
                        © {DateTime.UtcNow.Year} {appName}. Все права защищены.<br>
                        <a href=""{supportUrl}"" style=""color: {primaryColor}; text-decoration: none;"">Связаться с поддержкой</a>
                    </p>
                </td>
            </tr>
        </table>
        <style>
            @media only screen and (max-width: 600px) {{
                table {{ width: 100% !important; }}
                td {{ padding: 20px !important; }}
                h1 {{ font-size: 20px !important; }}
                .code {{ font-size: 20px !important; }}
            }}
        </style>
    </body>
    </html>";
    }

    public async Task<bool> VerifyCodeAsync(string email, string code, string type)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
        {
            _logger.LogWarning("User not found for email: {Email}", email);
            return false;
        }

        var verificationCode = await _context.VerificationCodes
            .FirstOrDefaultAsync(vc => vc.UserId == user.Id &&
                                      vc.Code == code &&
                                      vc.Type == type &&
                                      !vc.IsUsed &&
                                      vc.ExpiresAt > DateTime.UtcNow);

        if (verificationCode == null)
        {
            _logger.LogWarning("Invalid or expired code for email: {Email}, type: {Type}", email, type);
            return false;
        }

        verificationCode.IsUsed = true;
        await _context.SaveChangesAsync();
        _logger.LogInformation("Code verified for email: {Email}, type: {Type}", email, type);
        return true;
    }
}