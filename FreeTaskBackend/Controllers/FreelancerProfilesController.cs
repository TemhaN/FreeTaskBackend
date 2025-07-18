using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Stripe;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/users")]
public class FreelancerProfilesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FreeTaskBackend.Services.FileService _fileService;
    private readonly FreelancerProfileService _profileService;
    private readonly FreelancerLevelService _levelService;
    private readonly FreeTaskBackend.Services.ReviewService _reviewService;
    private readonly ILogger<FreelancerProfilesController> _logger;

    public FreelancerProfilesController(
        AppDbContext context,
        FreeTaskBackend.Services.FileService fileService,
        FreelancerProfileService profileService,
        FreelancerLevelService levelService,
        FreeTaskBackend.Services.ReviewService reviewService,
        ILogger<FreelancerProfilesController> logger)
    {
        _context = context;
        _fileService = fileService;
        _profileService = profileService;
        _levelService = levelService;
        _reviewService = reviewService;
        _logger = logger;
    }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetProfile(Guid id)
    {
        try
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", id);
                return NotFound(new { message = "Пользователь не найден" });
            }

            var response = new UserProfileResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Role = user.Role
            };

            if (user.Role == "Freelancer")
            {
                var profile = await _profileService.GetProfileAsync(id);
                response.Skills = profile.Skills;
                response.PortfolioItems = profile.PortfolioItems.Select(pi => new PortfolioItemResponseDto
                {
                    Id = pi.Id,
                    Url = pi.Url,
                    Description = pi.Description
                }).ToList();
                response.LevelPoints = profile.LevelPoints;
                response.Level = profile.Level;
                response.Rating = profile.Rating;
                response.AvatarUrl = profile.AvatarUrl;
                response.Bio = profile.Bio;
            }
            else if (user.Role == "Client")
            {
                var clientProfile = await _context.ClientProfiles.FirstOrDefaultAsync(cp => cp.UserId == id);
                if (clientProfile != null)
                {
                    response.Bio = clientProfile.Bio;
                    response.CompanyName = clientProfile.CompanyName;
                    response.AvatarUrl = clientProfile.AvatarUrl;
                }
            }

            _logger.LogInformation("Profile retrieved for user: {UserId}, Role: {Role}", id, user.Role);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile for user: {UserId}", id);
            return BadRequest(new { message = "Ошибка при получении профиля" });
        }
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProfile(Guid id, [FromForm] UpdateFreelancerProfileDto dto)
    {
        _logger.LogInformation("Processing UpdateProfile for user: {Id} at {Time}", id, DateTime.UtcNow);
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for UpdateProfile: {Errors}", ModelState);
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            _logger.LogWarning("No user ID claim found in token");
            return Unauthorized(new { message = "Неверный токен: отсутствует идентификатор пользователя" });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid user ID format in token: {UserIdClaim}", userIdClaim);
            return Unauthorized(new { message = "Неверный формат идентификатора пользователя в токене" });
        }

        if (id != userId)
        {
            _logger.LogWarning("User {UserId} attempted to update profile {Id} without permission", userId, id);
            return Forbid();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user?.Role != "Freelancer")
        {
            _logger.LogWarning("User {UserId} is not a Freelancer", userId);
            return BadRequest(new { message = "Только фрилансеры могут обновлять профиль" });
        }

        try
        {
            var profile = await _profileService.UpdateProfileAsync(id, dto);
            var response = new FreelancerProfileResponseDto
            {
                Id = profile.UserId,
                Email = user.Email,
                Name = user.Name,
                Skills = profile.Skills,
                Role = profile.User.Role ?? "Freelancer",
                PortfolioItems = profile.PortfolioItems.Select(pi => new PortfolioItemResponseDto
                {
                    Id = pi.Id,
                    Url = pi.Url,
                    Description = pi.Description
                }).ToList(),
                LevelPoints = profile.LevelPoints,
                Level = profile.Level,
                Rating = profile.Rating,
                AvatarUrl = profile.AvatarUrl,
                Bio = profile.Bio
            };
            _logger.LogInformation("Profile updated successfully for user: {UserId}", id);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user: {Id}", id);
            return BadRequest(new { ex.Message });
        }
    }
    [HttpGet("freelancers")]
    public async Task<IActionResult> SearchFreelancers([FromQuery] SearchFreelancersDto dto)
    {
        try
        {
            _logger.LogInformation("Searching freelancers with parameters: SearchTerm={SearchTerm}, MinRating={MinRating}, Level={Level}, UseFuzzySearch={UseFuzzySearch}",
                dto.SearchTerm, dto.MinRating, dto.Level, dto.UseFuzzySearch);

            var (freelancers, teams) = await _profileService.SearchFreelancersAsync(dto);
            var freelancerResponse = freelancers.Select(p => new FreelancerProfileResponseDto
            {
                Id = p.UserId,
                Email = p.User.Email,
                Name = p.User.Name,
                Skills = p.Skills,
                PortfolioItems = p.PortfolioItems.Select(pi => new PortfolioItemResponseDto
                {
                    Id = pi.Id,
                    Url = pi.Url,
                    Description = pi.Description
                }).ToList(),
                LevelPoints = p.LevelPoints,
                Level = p.Level,
                Rating = p.Rating,
                AvatarUrl = p.AvatarUrl,
                Bio = p.Bio,
                Role = "Freelancer"
            }).ToList();

            var teamResponse = teams.Select(t =>
            {
                List<string> skills = t.Skills ?? new List<string>();
                List<PortfolioItemDto> portfolio = new List<PortfolioItemDto>();

                try
                {
                    if (t.Portfolio != null)
                    {
                        portfolio = JsonSerializer.Deserialize<List<PortfolioItemDto>>(t.Portfolio.RootElement.GetRawText()) ?? new List<PortfolioItemDto>();
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize portfolio for team {TeamId}, using empty list", t.Id);
                }

                return new TeamResponseDto
                {
                    Id = t.Id,
                    Name = t.Name,
                    LeaderId = t.LeaderId,
                    LeaderName = t.Leader.Name,
                    Skills = skills,
                    Portfolio = portfolio,
                    Rating = t.Rating,
                    AvatarUrl = t.AvatarUrl
                };
            }).ToList();

            _logger.LogInformation("Found {FreelancerCount} freelancers and {TeamCount} teams", freelancerResponse.Count, teamResponse.Count);
            return Ok(new { Freelancers = freelancerResponse, Teams = teamResponse });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching freelancers and teams");
            return BadRequest(new { ex.Message });
        }
    }

    [Authorize]
    [HttpPost("freelancers/portfolio")]
    public async Task<IActionResult> AddPortfolioItem([FromForm] AddPortfolioItemDto dto)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for AddPortfolioItem: {Errors}", ModelState);
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            _logger.LogWarning("No nameidentifier claim found in token. Claims: {Claims}",
                string.Join(", ", User.Claims.Select(c => $"{c.Type}: {c.Value}")));
            return Unauthorized(new { message = "Неверный токен: отсутствует идентификатор пользователя" });
        }

        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid user ID format in token: {UserIdClaim}", userIdClaim);
            return Unauthorized(new { message = "Неверный формат идентификатора пользователя в токене" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Freelancer")
        {
            _logger.LogWarning("User {UserId} is not a Freelancer", userId);
            return BadRequest(new { message = "Только фрилансеры могут добавлять портфолио" });
        }

        try
        {
            var portfolioItem = await _profileService.AddPortfolioItemAsync(userId, dto);
            return Ok(portfolioItem);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding portfolio item for user: {UserId}", userId);
            return BadRequest(new { ex.Message });
        }
    }
    [Authorize]
    [HttpDelete("freelancers/portfolio/{portfolioId}")]
    public async Task<IActionResult> DeletePortfolioItem(Guid portfolioId)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid or missing user ID claim: {UserIdClaim}", userIdClaim);
            return Unauthorized(new { message = "Неверный токен: отсутствует идентификатор пользователя" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Freelancer")
        {
            _logger.LogWarning("User {UserId} is not a Freelancer", userId);
            return BadRequest(new { message = "Только фрилансеры могут удалять портфолио" });
        }

        try
        {
            await _profileService.DeletePortfolioItemAsync(userId, portfolioId);
            return Ok(new { message = "Работа удалена из портфолио" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting portfolio item for user: {UserId}", userId);
            return BadRequest(new { ex.Message });
        }
    }

    [Authorize]
    [HttpGet("{id}/analytics")]
    public async Task<IActionResult> GetProfileAnalytics(Guid id, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId) || id != userId)
            return Unauthorized(new { message = "Неверный токен или доступ запрещен" });

        try
        {
            var analytics = await _profileService.GetProfileAnalyticsAsync(id, startDate, endDate);
            return Ok(analytics);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving analytics for user: {UserId}", id);
            return BadRequest(new { message = "Ошибка при получении аналитики" });
        }
    }

    [HttpGet("skills/popular")]
    public async Task<IActionResult> GetPopularSkills([FromQuery] string prefix = "")
    {
        try
        {
            var skills = await _profileService.GetPopularSkillsAsync(prefix);
            return Ok(skills.Select(s => new { Skill = s.Key, Count = s.Value }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving popular skills for prefix: {Prefix}", prefix);
            return BadRequest(new { ex.Message });
        }
    }
    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> GetCurrentUserProfile()
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Invalid or missing user ID claim in token");
                return Unauthorized(new { message = "Неверный токен: отсутствует идентификатор пользователя" });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
            {
                _logger.LogWarning("User not found: {UserId}", userId);
                return NotFound(new { message = "Пользователь не найден" });
            }

            var response = new UserProfileResponseDto
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                Role = user.Role
            };

            if (user.Role == "Freelancer")
            {
                try
                {
                    var profile = await _profileService.GetProfileAsync(userId);
                    response.Skills = profile.Skills;
                    response.PortfolioItems = profile.PortfolioItems.Select(pi => new PortfolioItemResponseDto
                    {
                        Id = pi.Id,
                        Url = pi.Url,
                        Description = pi.Description
                    }).ToList();
                    response.LevelPoints = profile.LevelPoints;
                    response.Level = profile.Level;
                    response.Rating = profile.Rating;
                    response.AvatarUrl = profile.AvatarUrl;
                    response.Bio = profile.Bio;
                }
                catch (KeyNotFoundException)
                {
                    _logger.LogWarning("Freelancer profile not found for user: {UserId}", userId);
                }
            }
            else if (user.Role == "Client")
            {
                var clientProfile = await _context.ClientProfiles.FirstOrDefaultAsync(cp => cp.UserId == userId);
                if (clientProfile != null)
                {
                    response.Bio = clientProfile.Bio;
                    response.CompanyName = clientProfile.CompanyName;
                    response.AvatarUrl = clientProfile.AvatarUrl;
                }
            }

            _logger.LogInformation("Profile retrieved for user: {UserId}, Role: {Role}", userId, user.Role);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving profile");
            return BadRequest(new { message = "Ошибка при получении профиля" });
        }
    }

    [Authorize]
    [HttpPut("client/{id}")]
    public async Task<IActionResult> UpdateClientProfile(Guid id, [FromForm] UpdateClientProfileDto dto)
    {
        _logger.LogInformation("Processing UpdateClientProfile for user: {Id} at {Time}", id, DateTime.UtcNow);
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for UpdateClientProfile: {Errors}", ModelState);
            return BadRequest(ModelState);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId) || id != userId)
        {
            //_logger.LogWarning("User {UserId} attempted to update profile {Id} without permission", userId, id);
            return Unauthorized(new { message = "Неверный токен или доступ запрещён" });
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user?.Role != "Client")
        {

            _logger.LogWarning("User {UserId} is not a Client", userId);
            return BadRequest(new { message = "Только клиенты могут обновлять профиль клиента" });
        }

        try
        {
            var clientProfile = await _context.ClientProfiles.FirstOrDefaultAsync(cp => cp.UserId == id);
            if (clientProfile == null)
            {
                if (dto.Bio != null) clientProfile.Bio = dto.Bio;
                if (dto.CompanyName != null) clientProfile.CompanyName = dto.CompanyName;

                clientProfile = new ClientProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = id,
                    CompanyName = dto.CompanyName,
                    Bio = dto.Bio,
                };
                _context.ClientProfiles.Add(clientProfile);
            }
            else
            {
                clientProfile.CompanyName = dto.CompanyName ?? clientProfile.CompanyName;
                clientProfile.Bio = dto.Bio ?? clientProfile.Bio;
            }

            if (dto.Avatar != null)
            {
                var fileUrl = await _fileService.UploadFileAsync(dto.Avatar, "avatars", id);
                clientProfile.AvatarUrl = fileUrl;
            }

            await _context.SaveChangesAsync();

            var response = new ClientProfileResponseDto
            {
                Id = clientProfile.Id,
                UserId = clientProfile.UserId,
                Email = user.Email,
                CompanyName = clientProfile.CompanyName,
                Bio = clientProfile.Bio,
                AvatarUrl = clientProfile.AvatarUrl
            };

            _logger.LogInformation("Client profile updated successfully for user: {UserId}", id);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating client profile for user: {Id}", id);
            return BadRequest(new { ex.Message });
        }
    }
    [Authorize]
    [HttpPost("{id}/recalculate-level")]
    public async Task<IActionResult> RecalculateLevel(Guid id)
    {
        _logger.LogInformation("Processing RecalculateLevel for user: {Id} at {Time}", id, DateTime.UtcNow);

        // Проверка авторизации
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            _logger.LogWarning("Invalid or missing user ID claim in token");
            return Unauthorized(new { message = "Неверный токен: отсутствует идентификатор пользователя" });
        }

        // Проверка соответствия ID
        if (id != userId)
        {
            _logger.LogWarning("User {UserId} attempted to recalculate level for profile {Id} without permission", userId, id);
            return Forbid();
        }

        // Проверка роли пользователя
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user?.Role != "Freelancer")
        {
            _logger.LogWarning("User {UserId} is not a Freelancer", userId);
            return BadRequest(new { message = "Только фрилансеры могут пересчитывать уровень" });
        }

        try
        {
            // Пересчет очков и уровня
            await _levelService.UpdateFreelancerLevelAsync(id);

            // Получение обновленного профиля
            var profile = await _profileService.GetProfileAsync(id);
            var response = new FreelancerProfileResponseDto
            {
                Id = profile.UserId,
                Email = profile.User.Email,
                Name = profile.User.Name,
                Role = profile.User.Role,
                Skills = profile.Skills,
                PortfolioItems = profile.PortfolioItems.Select(pi => new PortfolioItemResponseDto
                {
                    Id = pi.Id,
                    Url = pi.Url,
                    Description = pi.Description
                }).ToList(),
                LevelPoints = profile.LevelPoints,
                Level = profile.Level,
                Rating = profile.Rating,
                AvatarUrl = profile.AvatarUrl,
                Bio = profile.Bio
            };

            _logger.LogInformation("Level recalculated successfully for user: {UserId}", id);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Profile not found for user: {UserId}", id);
            return NotFound(new { ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recalculating level for user: {Id}", id);
            return BadRequest(new { ex.Message });
        }
    }
    [HttpGet("{id}/reviews")]
    public async Task<IActionResult> GetFreelancerReviews(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var reviews = await _reviewService.GetFreelancerReviewsAsync(id, page, pageSize);
            _logger.LogInformation("Retrieved {ReviewCount} reviews for freelancer: {FreelancerId}", reviews.Count, id);
            return Ok(reviews);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for freelancer: {FreelancerId}", id);
            return BadRequest(new { ex.Message });
        }
    }

}