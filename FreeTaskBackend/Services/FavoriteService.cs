using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class FavoriteService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FavoriteService> _logger;

    public FavoriteService(AppDbContext context, ILogger<FavoriteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<(Favorite? Favorite, string Action)> ToggleFavoriteAsync(Guid clientId, AddFavoriteDto dto)
    {
        var client = await _context.Users.FirstOrDefaultAsync(u => u.Id == clientId && u.Role == "Client");
        if (client == null)
        {
            _logger.LogWarning("Client not found: {ClientId}", clientId);
            throw new KeyNotFoundException("Клиент не найден");
        }

        if (dto.FreelancerId == null && dto.TeamId == null)
        {
            _logger.LogWarning("Both FreelancerId and TeamId are null for client: {ClientId}", clientId);
            throw new ArgumentException("Необходимо указать FreelancerId или TeamId");
        }

        if (dto.FreelancerId != null && dto.TeamId != null)
        {
            _logger.LogWarning("Both FreelancerId and TeamId provided for client: {ClientId}", clientId);
            throw new ArgumentException("Нельзя указать одновременно FreelancerId и TeamId");
        }

        Favorite? existing = null;
        if (dto.FreelancerId != null)
        {
            var freelancer = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.FreelancerId && u.Role == "Freelancer");
            if (freelancer == null)
            {
                _logger.LogWarning("Freelancer not found: {FreelancerId}", dto.FreelancerId);
                throw new KeyNotFoundException("Фрилансер не найден");
            }

            existing = await _context.Favorites.FirstOrDefaultAsync(f =>
                f.ClientId == clientId &&
                f.FreelancerId != null &&
                f.FreelancerId.ToString().ToLower() == dto.FreelancerId.Value.ToString().ToLower());
        }
        else if (dto.TeamId != null)
        {
            var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == dto.TeamId);
            if (team == null)
            {
                _logger.LogWarning("Team not found: {TeamId}", dto.TeamId);
                throw new KeyNotFoundException("Команда не найдена");
            }

            existing = await _context.Favorites.FirstOrDefaultAsync(f =>
                f.ClientId == clientId &&
                f.TeamId != null &&
                f.TeamId.ToString().ToLower() == dto.TeamId.Value.ToString().ToLower());
        }

        if (existing != null)
        {
            _context.Favorites.Remove(existing);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Favorite removed for client: {ClientId}, Favorite: {FavoriteId}", clientId, existing.Id);
            return (null, "removed");
        }
        else
        {
            var favorite = new Favorite
            {
                Id = Guid.NewGuid(),
                ClientId = clientId,
                FreelancerId = dto.FreelancerId,
                TeamId = dto.TeamId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Favorites.Add(favorite);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Favorite added for client: {ClientId}, Freelancer: {FreelancerId}, Team: {TeamId}", clientId, dto.FreelancerId, dto.TeamId);
            return (favorite, "added");
        }
    }

    public async Task<List<Favorite>> GetFavoritesAsync(Guid clientId, int page, int pageSize)
    {
        var client = await _context.Users.FirstOrDefaultAsync(u => u.Id == clientId && u.Role == "Client");
        if (client == null)
        {
            _logger.LogWarning("Client not found: {ClientId}", clientId);
            throw new KeyNotFoundException("Клиент не найден");
        }

        var favorites = await _context.Favorites
            .Where(f => f.ClientId == clientId)
            .Include(f => f.Freelancer)
            .Include(f => f.Team)
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation("Retrieved {Count} favorites for client: {ClientId}", favorites.Count, clientId);
        return favorites;
    }
}