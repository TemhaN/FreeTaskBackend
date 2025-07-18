using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class TeamService
{
    private readonly AppDbContext _context; 
    private readonly FileService _fileService; 
    private readonly ILogger _logger;
    public TeamService(AppDbContext context, FileService fileService, ILogger<TeamService> logger)
    {
        _context = context;
        _fileService = fileService;
        _logger = logger;
    }

    public async Task<TeamResponseDto> CreateTeamAsync(Guid userId, CreateTeamDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || !user.Role.Contains("Freelancer"))
            throw new UnauthorizedAccessException("Only freelancers can create teams");

        // Проверка на наличие другой команды
        var existingTeam = await _context.Teams.FirstOrDefaultAsync(t => t.LeaderId == userId);
        if (existingTeam != null)
            throw new InvalidOperationException("User already leads a team");

        List<string> skills = new List<string>();
        if (!string.IsNullOrEmpty(dto.SkillsString))
        {
            try
            {
                skills = JsonSerializer.Deserialize<List<string>>(dto.SkillsString);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize SkillsString for user: {UserId}", userId);
                throw new ArgumentException("Invalid Skills format");
            }
        }

        JsonDocument portfolio = JsonDocument.Parse("[]");
        if (!string.IsNullOrEmpty(dto.PortfolioString))
        {
            try
            {
                portfolio = JsonDocument.Parse(dto.PortfolioString);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize PortfolioString for user: {UserId}", userId);
                throw new ArgumentException("Invalid Portfolio format");
            }
        }

        string? avatarUrl = null;
        if (dto.Avatar != null)
        {
            if (dto.Avatar.Length > 5 * 1024 * 1024)
                throw new ArgumentException("Avatar file size must be less than 5MB");
            if (!new[] { "image/jpeg", "image/png" }.Contains(dto.Avatar.ContentType))
                throw new ArgumentException("Avatar must be JPEG or PNG");

            var fileName = $"{Guid.NewGuid()}_{dto.Avatar.FileName}";
            var filePath = Path.Combine("wwwroot/uploads/avatars", fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await dto.Avatar.CopyToAsync(stream);
            }
            avatarUrl = $"/uploads/avatars/{fileName}";
        }

        var team = new Team
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            LeaderId = userId,
            Skills = skills,
            Portfolio = portfolio,
            AvatarUrl = avatarUrl ?? "",
            Rating = 0,
        };

        _context.Teams.Add(team);
        await _context.SaveChangesAsync();

        return new TeamResponseDto
        {
            Id = team.Id,
            Name = team.Name,
            LeaderId = team.LeaderId,
            LeaderName = user.Name,
            Skills = team.Skills,
            Portfolio = team.Portfolio != null
                ? (JsonSerializer.Deserialize<List<PortfolioItemDto>>(team.Portfolio.RootElement.GetRawText()) ?? new List<PortfolioItemDto>())
                : new List<PortfolioItemDto>(),
            AvatarUrl = team.AvatarUrl,
            Rating = team.Rating,
        };
    }

    // Новый метод для удаления команды
    public async Task DeleteTeamAsync(Guid teamId, Guid leaderId)
    {
        var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == teamId)
            ?? throw new KeyNotFoundException("Команда не найдена");

        if (team.LeaderId != leaderId)
            throw new UnauthorizedAccessException("Только лидер может удалить команду");

        // Удаляем связанные записи
        var members = await _context.TeamMembers.Where(m => m.TeamId == teamId).ToListAsync();
        _context.TeamMembers.RemoveRange(members);

        var bids = await _context.Bids.Where(b => b.TeamId == teamId).ToListAsync();
        _context.Bids.RemoveRange(bids);

        // Удаляем аватар, если он есть
        if (!string.IsNullOrEmpty(team.AvatarUrl))
        {
            var fileId = Path.GetFileNameWithoutExtension(team.AvatarUrl.Split('/').Last());
            if (Guid.TryParse(fileId, out var guidFileId))
            {
                await _fileService.DeleteFileAsync(guidFileId);
                _logger.LogInformation("Deleted avatar for team: {TeamId}, FileId: {FileId}", teamId, guidFileId);
            }
        }

        _context.Teams.Remove(team);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Team deleted: {TeamId} by leader: {LeaderId}", teamId, leaderId);
    }

    public async Task UpdateTeamPortfolioAsync(Guid teamId, Guid leaderId, UpdateTeamPortfolioDto dto)
    {
        var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            throw new KeyNotFoundException("Team not found");
        if (team.LeaderId != leaderId)
            throw new UnauthorizedAccessException("Only the team leader can update the portfolio");

        var portfolioItems = dto.Items ?? new List<PortfolioItemDto>();
        if (dto.File != null)
        {
            var fileUrl = await _fileService.UploadFileAsync(dto.File, "team_portfolios", teamId);
            portfolioItems.Add(new PortfolioItemDto
            {
                Title = dto.File.FileName,
                Description = dto.Description,
                FileUrl = fileUrl
            });
        }

        team.Portfolio = JsonDocument.Parse(JsonSerializer.Serialize(portfolioItems));

        if (dto.Avatar != null)
        {
            try
            {
                // Удаляем старую аватарку, если она существует
                if (!string.IsNullOrEmpty(team.AvatarUrl))
                {
                    var fileId = Path.GetFileNameWithoutExtension(team.AvatarUrl).Split('/').Last();
                    await _fileService.DeleteFileAsync(Guid.Parse(fileId));
                }
                team.AvatarUrl = await _fileService.UploadFileAsync(dto.Avatar, "team_avatars", teamId);
                _logger.LogInformation("Avatar updated for team: {TeamId}, URL: {AvatarUrl}", teamId, team.AvatarUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating avatar for team: {TeamId}", teamId);
                throw new Exception("Failed to update avatar");
            }
        }

        await _context.SaveChangesAsync();
    }
    public async Task<TeamResponseDto> GetTeamByIdAsync(Guid id)
    {
        var team = await _context.Teams
            .Include(t => t.Leader)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (team == null)
            return null;

        List<PortfolioItemDto> portfolio = new List<PortfolioItemDto>();
        try
        {
            if (team.Portfolio != null)
            {
                portfolio = JsonSerializer.Deserialize<List<PortfolioItemDto>>(team.Portfolio.RootElement.GetRawText()) ?? new List<PortfolioItemDto>();
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize portfolio for team {TeamId}", id);
            portfolio = new List<PortfolioItemDto>();
        }

        return new TeamResponseDto
        {
            Id = team.Id,
            Name = team.Name,
            LeaderId = team.LeaderId,
            LeaderName = team.Leader?.Name ?? "Unknown",
            Skills = team.Skills ?? new List<string>(),
            Portfolio = portfolio,
            Rating = team.Rating,
            AvatarUrl = team.AvatarUrl
        };
    }
    public async Task<List<TeamResponseDto>> GetTeamsAsync(TeamFilterDto dto)
    {
        var query = _context.Teams
            .Include(t => t.Leader)
            .AsQueryable();

        if (!string.IsNullOrEmpty(dto.Skills))
        {
            var skills = dto.Skills
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLower())
                .ToList();
            query = query.Where(t => t.Skills.Any(s => skills.Contains(s.ToLower())));
        }

        var teams = await query
            .Skip((dto.Page - 1) * dto.PageSize)
            .Take(dto.PageSize)
            .ToListAsync();

        return teams.Select(t => new TeamResponseDto
        {
            Id = t.Id,
            Name = t.Name,
            LeaderId = t.LeaderId,
            LeaderName = t.Leader?.Name ?? "Unknown",
            Skills = t.Skills ?? new List<string>(),
            Portfolio = t.Portfolio != null
                ? (JsonSerializer.Deserialize<List<PortfolioItemDto>>(t.Portfolio.RootElement.GetRawText()) ?? new List<PortfolioItemDto>())
                : new List<PortfolioItemDto>(),
            Rating = t.Rating,
            AvatarUrl = t.AvatarUrl
        }).ToList();
    }


    public async Task AddMemberAsync(Guid teamId, Guid leaderId, AddTeamMemberDto dto)
    {
        var team = await _context.Teams.FindAsync(teamId)
            ?? throw new KeyNotFoundException("Команда не найдена");

        if (team.LeaderId != leaderId)
            throw new UnauthorizedAccessException("Только лидер может добавлять участников");

        var member = new TeamMember
        {
            TeamId = teamId,
            FreelancerId = dto.FreelancerId,
            Role = dto.Role,
            BudgetShare = dto.BudgetShare
        };
        _context.TeamMembers.Add(member);
        await _context.SaveChangesAsync();
    }

    public async Task<BidResponseDto> PlaceTeamBidAsync(Guid teamId, Guid userId, PlaceTeamBidDto dto)
    {
        var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            throw new KeyNotFoundException("Team not found");

        var freelancer = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId && u.Role == "Freelancer");
        if (freelancer == null)
            throw new KeyNotFoundException("Freelancer not found");

        if (dto.Type == "Order" && !dto.OrderId.HasValue)
            throw new InvalidOperationException("OrderId is required for order bids");

        if (dto.Type == "Membership")
        {
            // Проверка, не состоит ли уже в команде
            var isMember = await _context.TeamMembers.AnyAsync(tm => tm.TeamId == teamId && tm.FreelancerId == userId);
            if (isMember)
                throw new InvalidOperationException("User is already a member of this team");

            // Проверка, нет ли активной заявки
            var existingBid = await _context.Bids
                .AnyAsync(b => b.TeamId == teamId && b.FreelancerId == userId && b.Type == "Membership" && b.Status == "Pending");
            if (existingBid)
                throw new InvalidOperationException("Membership bid already exists");
        }

        var bid = new Bid
        {
            Id = Guid.NewGuid(),
            OrderId = dto.OrderId, 
            TeamId = teamId,
            FreelancerId = userId,
            Amount = dto.Amount,
            Comment = dto.Comment ?? "",
            Type = dto.Type,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Bids.Add(bid);
        await _context.SaveChangesAsync();

        return new BidResponseDto
        {
            Id = bid.Id,
            OrderId = bid.OrderId,
            TeamId = bid.TeamId,
            FreelancerId = bid.FreelancerId,
            FreelancerName = freelancer.Name,
            Amount = bid.Amount,
            Comment = bid.Comment,
            Type = bid.Type,
            Status = bid.Status,
            CreatedAt = bid.CreatedAt
        };
    }
    public async Task<BidResponseDto> HandleTeamBidAsync(Guid teamId, Guid bidId, Guid leaderId, AcceptTeamBidDto dto)
    {
        var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            throw new KeyNotFoundException("Team not found");

        if (team.LeaderId != leaderId)
            throw new UnauthorizedAccessException("Only the team leader can handle bids");

        var bid = await _context.Bids
            .Include(b => b.Freelancer)
            .FirstOrDefaultAsync(b => b.Id == bidId && b.TeamId == teamId);
        if (bid == null)
            throw new KeyNotFoundException("Bid not found");

        if (bid.Status != "Pending")
            throw new InvalidOperationException("Bid is already processed");

        bid.Status = dto.Accept ? "Accepted" : "Rejected";
        if (dto.Accept && bid.Type == "Membership")
        {
            var member = new TeamMember
            {
                TeamId = teamId,
                FreelancerId = bid.FreelancerId,
                Role = bid.Comment,
                BudgetShare = bid.Amount
            };
            _context.TeamMembers.Add(member);
        }

        await _context.SaveChangesAsync();

        return new BidResponseDto
        {
            Id = bid.Id,
            OrderId = bid.OrderId,
            TeamId = bid.TeamId,
            FreelancerId = bid.FreelancerId,
            FreelancerName = bid.Freelancer.Name,
            Amount = bid.Amount,
            Comment = bid.Comment,
            Type = bid.Type,
            Status = bid.Status,
            CreatedAt = bid.CreatedAt
        };
    }
    public async Task<List<BidResponseDto>> GetTeamBidsAsync(Guid teamId, Guid leaderId)
    {
        var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == teamId);
        if (team == null)
            throw new KeyNotFoundException("Team not found");

        if (team.LeaderId != leaderId)
            throw new UnauthorizedAccessException("Only the team leader can view bids");

        var bids = await _context.Bids
            .Include(b => b.Freelancer)
            .Where(b => b.TeamId == teamId && b.Type == "Membership" && b.Status == "Pending")
            .Select(b => new BidResponseDto
            {
                Id = b.Id,
                OrderId = b.OrderId,
                TeamId = b.TeamId,
                FreelancerId = b.FreelancerId,
                FreelancerName = b.Freelancer.Name,
                Amount = b.Amount,
                Comment = b.Comment,
                Type = b.Type,
                Status = b.Status,
                CreatedAt = b.CreatedAt
            })
            .ToListAsync();

        return bids;
    }
    public async Task RemoveMemberAsync(Guid teamId, Guid leaderId, Guid userId)
    {
        var team = await _context.Teams.FindAsync(teamId)
            ?? throw new KeyNotFoundException("Команда не найдена");

        if (team.LeaderId != leaderId)
            throw new UnauthorizedAccessException("Только лидер может удалять участников");

        var member = await _context.TeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.FreelancerId == userId)
            ?? throw new KeyNotFoundException("Участник не найден");

        _context.TeamMembers.Remove(member);
        await _context.SaveChangesAsync();
    }

}