using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/email")]
public class EmailVerificationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly EmailService _emailService;
    private readonly ILogger<EmailVerificationController> _logger;

    public EmailVerificationController(AppDbContext context, EmailService emailService, ILogger<EmailVerificationController> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    [HttpPost("verify")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        if (user.IsEmailVerified)
            return BadRequest(new { message = "Email уже подтвержден" });

        var isValid = await _emailService.VerifyCodeAsync(dto.Email, dto.Code, "EmailVerification");
        if (!isValid)
            return BadRequest(new { message = "Неверный или истекший код" });

        user.IsEmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Email verified for user: {Email}", dto.Email);
        return Ok(new { message = "Email успешно подтвержден" });
    }

    [HttpGet("verify-link")]
    public async Task<IActionResult> VerifyEmailLink([FromQuery] string email, [FromQuery] string code)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
            return BadRequest(new { message = "Email или код отсутствуют" });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null)
            return NotFound(new { message = "Пользователь не найден" });

        if (user.IsEmailVerified)
            return BadRequest(new { message = "Email уже подтвержден" });

        var isValid = await _emailService.VerifyCodeAsync(email, code, "EmailVerification");
        if (!isValid)
            return BadRequest(new { message = "Неверный или истекший код" });

        user.IsEmailVerified = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Email verified via link for user: {Email}", email);
        return Ok(new { message = "Email успешно подтвержден" });
    }

    [HttpPost("resend-code")]
    public async Task<IActionResult> ResendVerificationCode([FromBody] RequestVerificationCodeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null)
        {
            _logger.LogInformation("Resend code requested for non-existent user: {Email}", dto.Email);
            return Ok(new { message = "Если email зарегистрирован, код отправлен" });
        }

        if (dto.Type == "EmailVerification" && user.IsEmailVerified)
            return BadRequest(new { message = "Email уже подтвержден" });

        if (dto.Type == "PasswordReset" && user.GoogleId != null)
            return BadRequest(new { message = "Пользователь использует Google-авторизацию" });

        if (dto.Type != "EmailVerification" && dto.Type != "PasswordReset")
            return BadRequest(new { message = "Недопустимый тип кода" });

        var sent = await _emailService.SendVerificationCodeAsync(dto.Email, dto.Type);
        if (!sent)
            return BadRequest(new { message = "Превышен лимит запросов кода. Попробуйте снова завтра." });

        _logger.LogInformation("Verification code of type {Type} resent to: {Email}", dto.Type, dto.Email);
        return Ok(new { message = "Новый код отправлен на вашу почту" });
    }

    [HttpPost("reset-password/request")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null || user.GoogleId != null)
        {
            _logger.LogInformation("Password reset requested for non-existent or Google user: {Email}", dto.Email);
            return Ok(new { message = "Если email зарегистрирован, код отправлен" });
        }

        var sent = await _emailService.SendVerificationCodeAsync(dto.Email, "PasswordReset");
        if (!sent)
            return BadRequest(new { message = "Превышен лимит запросов кода. Попробуйте снова завтра." });

        _logger.LogInformation("Password reset code sent to: {Email}", dto.Email);
        return Ok(new { message = "Если email зарегистрирован, код отправлен" });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == dto.Email);
        if (user == null || user.GoogleId != null)
            return BadRequest(new { message = "Пользователь не найден или использует Google-авторизацию" });

        var isValid = await _emailService.VerifyCodeAsync(dto.Email, dto.Code, "PasswordReset");
        if (!isValid)
            return BadRequest(new { message = "Неверный или истекший код" });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Password reset for user: {Email}", dto.Email);
        return Ok(new { message = "Пароль успешно сброшен" });
    }
}