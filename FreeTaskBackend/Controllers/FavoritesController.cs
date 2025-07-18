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
[Route("api/v1/favorites")]
public class FavoritesController : ControllerBase
{
    private readonly FavoriteService _favoriteService;
    private readonly AppDbContext _context;
    private readonly ILogger<FavoritesController> _logger;

    public FavoritesController(FavoriteService favoriteService, AppDbContext context, ILogger<FavoritesController> logger)
    {
        _favoriteService = favoriteService;
        _context = context;
        _logger = logger;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ToggleFavorite([FromBody] AddFavoriteDto dto)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for ToggleFavorite: {Errors}", ModelState);
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid user ID in token: {UserIdClaim}", userIdClaim);
            return Unauthorized(new { message = "Неверный токен" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Client")
        {
            _logger.LogWarning("User is not a client: {UserId}", userId);
            return BadRequest(new { message = "Только клиенты могут управлять избранным" });
        }

        try
        {
            var (favorite, action) = await _favoriteService.ToggleFavoriteAsync(userId, dto);
            var response = new FavoriteResponseDto
            {
                Id = favorite?.Id ?? Guid.Empty,
                ClientId = userId,
                FreelancerId = favorite?.FreelancerId,
                FreelancerName = favorite?.Freelancer?.Name,
                TeamId = favorite?.TeamId,
                TeamName = favorite?.Team?.Name,
                CreatedAt = favorite?.CreatedAt ?? DateTime.UtcNow,
                Action = action // "added" или "removed"
            };
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling favorite for client: {UserId}", userId);
            return BadRequest(new { ex.Message });
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetFavorites([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid user ID in token: {UserIdClaim}", userIdClaim);
            return Unauthorized(new { message = "Неверный токен" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Client")
        {
            _logger.LogWarning("User is not a client: {UserId}", userId);
            return BadRequest(new { message = "Только клиенты могут просматривать избранное" });
        }

        try
        {
            var favorites = await _favoriteService.GetFavoritesAsync(userId, page, pageSize);
            var response = favorites.Select(f => new FavoriteResponseDto
            {
                Id = f.Id,
                ClientId = f.ClientId,
                FreelancerId = f.FreelancerId,
                FreelancerName = f.Freelancer?.Name,
                TeamId = f.TeamId,
                TeamName = f.Team?.Name,
                CreatedAt = f.CreatedAt,
                Action = "existing"
            }).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving favorites for client: {UserId}", userId);
            return BadRequest(new { ex.Message });
        }
    }
}