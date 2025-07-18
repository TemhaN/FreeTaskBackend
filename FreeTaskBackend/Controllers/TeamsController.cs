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
[Route("api/v1/teams")]
public class TeamsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly TeamService _teamService;
    private readonly UserService _userService;
    private readonly ILogger<TeamsController> _logger;

    public TeamsController(AppDbContext context, UserService userService, TeamService teamService, ILogger<TeamsController> logger)
    {
        _context = context;
        _teamService = teamService;
        _userService = userService;
        _logger = logger;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateTeam([FromForm] CreateTeamDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token" });

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for CreateTeam: {Errors}", ModelState);
            return BadRequest(ModelState);
        }

        try
        {
            var team = await _teamService.CreateTeamAsync(userId, dto);
            return Ok(team);
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Invalid input for creating team for user: {UserId}", userId);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating team for user: {UserId}", userId);
            return BadRequest(new { message = "An error occurred while creating the team." });
        }
    }
    [HttpGet]
    public async Task<IActionResult> GetTeams([FromQuery] TeamFilterDto dto)
    {
        try
        {
            var teams = await _teamService.GetTeamsAsync(dto);
            return Ok(teams);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving teams");
            return BadRequest(new { ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTeamById(Guid id)
    {
        try
        {
            var team = await _teamService.GetTeamByIdAsync(id);
            if (team == null)
                return NotFound(new { message = "Team not found" });
            return Ok(team);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving team {TeamId}", id);
            return BadRequest(new { ex.Message });
        }
    }
    [Authorize]
    [HttpPost("{id}/members")]
    public async Task<IActionResult> AddMember(Guid id, [FromBody] AddTeamMemberDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token" });

        var freelancer = await _userService.GetUserAsync(dto.FreelancerId);
        if (freelancer == null || !freelancer.Role.Contains("Freelancer"))
            return NotFound("Freelancer not found.");

        try
        {
            await _teamService.AddMemberAsync(id, userId, dto);
            return Ok(new { message = "Member added" });
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
            _logger.LogError(ex, "Error adding member to team: {TeamId}", id);
            return BadRequest(new { ex.Message });
        }
    }

    [Authorize]
    [HttpPost("{id}/bids")]
    public async Task<IActionResult> PlaceTeamBid(Guid id, [FromBody] PlaceTeamBidDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token" });

        try
        {
            var bid = await _teamService.PlaceTeamBidAsync(id, userId, dto);
            return Ok(bid);
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
            _logger.LogError(ex, "Error placing bid for team: {TeamId}", id);
            return BadRequest(new { ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{id}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        var leaderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(leaderIdClaim, out var leaderId))
            return Unauthorized(new { message = "Invalid token" });

        try
        {
            await _teamService.RemoveMemberAsync(id, leaderId, userId);
            return Ok(new { message = "Member removed" });
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
            _logger.LogError(ex, "Error removing member {UserId} from team: {TeamId}", userId, id);
            return BadRequest(new { ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id}/portfolio")]
    public async Task<IActionResult> UpdateTeamPortfolio(Guid id, [FromForm] UpdateTeamPortfolioDto dto)
    {
        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Invalid model state for UpdateTeamPortfolio: {Errors}", ModelState);
            return BadRequest(ModelState);
        }

        var leaderIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(leaderIdClaim, out var leaderId))
            return Unauthorized(new { message = "Invalid token" });

        if (dto.Items.Count > 10)
            return BadRequest(new { message = "Portfolio cannot exceed 10 items" });

        try
        {
            await _teamService.UpdateTeamPortfolioAsync(id, leaderId, dto);
            return Ok(new { message = "Team portfolio updated" });
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
            _logger.LogError(ex, "Error updating portfolio for team: {TeamId}", id);
            return BadRequest(new { ex.Message });
        }
    }
    [Authorize]
    [HttpPost("{id}/bids/{bidId}/accept")]
    public async Task<IActionResult> HandleTeamBid(Guid id, Guid bidId, [FromBody] AcceptTeamBidDto dto)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token" });

        try
        {
            var result = await _teamService.HandleTeamBidAsync(id, bidId, userId, dto);
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling bid {BidId} for team: {TeamId}", bidId, id);
            return BadRequest(new { ex.Message });
        }
    }
    [Authorize]
    [HttpGet("{id}/bids")]
    public async Task<IActionResult> GetTeamBids(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token" });

        try
        {
            var bids = await _teamService.GetTeamBidsAsync(id, userId);
            return Ok(bids);
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
            _logger.LogError(ex, "Error retrieving bids for team: {TeamId}", id);
            return BadRequest(new { ex.Message });
        }
    }
    // Новый эндпоинт для удаления команды
    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTeam(Guid id)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Invalid token" });

        try
        {
            await _teamService.DeleteTeamAsync(id, userId);
            return Ok(new { message = "Team deleted" });
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
            _logger.LogError(ex, "Error deleting team: {TeamId}", id);
            return BadRequest(new { ex.Message });
        }
    }
}