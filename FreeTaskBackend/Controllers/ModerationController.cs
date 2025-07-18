using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/moderation")]
public class ModerationController : ControllerBase
{
    private readonly ModerationService _moderationService;
    private readonly ILogger<ModerationController> _logger;

    public ModerationController(ModerationService moderationService, ILogger<ModerationController> logger)
    {
        _moderationService = moderationService;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("report")]
    public async Task<IActionResult> ReportContent([FromBody] ReportContentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });
        var exists = await _moderationService.TargetExistsAsync(dto.TargetType, dto.TargetId);
        if (!exists)
            return BadRequest("Target does not exist.");
        try
        {
            await _moderationService.ReportContentAsync(userId, dto);
            return Ok(new { message = "Жалоба отправлена" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting content for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при отправке жалобы" });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingReports([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var reports = await _moderationService.GetPendingReportsAsync(page, pageSize);
            var response = reports.Select(r => new ReportResponseDto
            {
                Id = r.Id,
                ReporterId = r.ReporterId,
                TargetId = r.TargetId,
                TargetType = r.TargetType,
                Reason = r.Reason,
                CreatedAt = r.CreatedAt
            }).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending reports");
            return BadRequest(new { message = "Ошибка при получении жалоб" });
        }
    }
}