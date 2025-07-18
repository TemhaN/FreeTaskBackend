using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FreeTaskBackend.Services;

public class FreelancerProfileService
{
    private readonly AppDbContext _context;
    private readonly FileService _fileService;
    private readonly IConfiguration _configuration;
    private readonly FreelancerLevelService _levelService;
    private readonly ILogger<FreelancerProfileService> _logger;

    public FreelancerProfileService(
        AppDbContext context,
        FileService fileService,
        IConfiguration configuration,
        FreelancerLevelService levelService,
        ILogger<FreelancerProfileService> logger)
    {
        _context = context;
        _fileService = fileService;
        _configuration = configuration;
        _levelService = levelService;
        _logger = logger;
    }

    public async Task<FreelancerProfile> GetProfileAsync(Guid userId)
    {
        var profile = await _context.FreelancerProfiles
            .Include(p => p.User)
            .Include(p => p.PortfolioItems)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            _logger.LogWarning("Profile not found for user: {UserId}", userId);
            throw new KeyNotFoundException("Профиль фрилансера не найден");
        }

        // Пересчитываем уровень и очки через FreelancerLevelService
        await _levelService.UpdateFreelancerLevelAsync(userId);

        // Пересчитываем рейтинг на основе отзывов
        var averageRating = await _context.Reviews
            .Where(r => r.Order.FreelancerId == userId)
            .AverageAsync(r => (decimal?)r.Rating) ?? 0;

        profile.Rating = (decimal)Math.Round(averageRating, 2);
        _context.Entry(profile).State = EntityState.Modified;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Profile retrieved for user: {UserId}, Rating: {Rating}, Level: {Level}, LevelPoints: {LevelPoints}",
            userId, profile.Rating, profile.Level, profile.LevelPoints);

        return profile;
    }

    public async Task<FreelancerProfile> UpdateProfileAsync(Guid userId, UpdateFreelancerProfileDto dto)
    {
        var profile = await _context.FreelancerProfiles
            .Include(p => p.User)
            .Include(p => p.PortfolioItems) // Добавлено для загрузки PortfolioItems
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            _logger.LogWarning("Profile not found for user: {UserId}", userId);
            throw new KeyNotFoundException("Профиль не найден");
        }

        try
        {
            // Обновляем навыки
            if (dto.Skills != null)
            {
                List<string> skills;
                try
                {
                    if (dto.Skills.Count == 1 && dto.Skills.First().StartsWith("["))
                    {
                        skills = JsonSerializer.Deserialize<List<string>>(dto.Skills.First());
                    }
                    else
                    {
                        skills = dto.Skills;
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize skills for user: {UserId}, using raw input", userId);
                    skills = dto.Skills;
                }

                profile.Skills = skills.Take(10).Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                _logger.LogInformation("Updated skills for user: {UserId}, Skills: {Skills}", userId, string.Join(", ", profile.Skills));
            }

            // Обновляем биографию
            if (dto.Bio != null)
            {
                profile.Bio = dto.Bio.Length > 500 ? dto.Bio[..500] : dto.Bio; // Ограничиваем до 500 символов
                _logger.LogInformation("Updated bio for user: {UserId}, Bio: {Bio}", userId, dto.Bio);
            }

            // Обновляем аватар
            if (dto.Avatar != null)
            {
                try
                {
                    // Удаляем старый аватар, если он существует
                    if (!string.IsNullOrEmpty(profile.AvatarUrl))
                    {
                        var oldAvatarId = Path.GetFileNameWithoutExtension(profile.AvatarUrl.Split('/').Last());
                        if (Guid.TryParse(oldAvatarId, out var fileId))
                        {
                            await _fileService.DeleteFileAsync(fileId);
                            _logger.LogInformation("Deleted old avatar for user: {UserId}, FileId: {FileId}", userId, fileId);
                        }
                    }

                    // Загружаем новый аватар
                    var filePath = await _fileService.UploadFileAsync(dto.Avatar, "avatars", userId);
                    profile.AvatarUrl = filePath;
                    _logger.LogInformation("Avatar uploaded for user: {UserId}, URL: {AvatarUrl}", userId, filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading avatar for user: {UserId}", userId);
                    throw new Exception("Ошибка при загрузке аватара", ex);
                }
            }

            // Сохраняем изменения
            _context.Entry(profile).State = EntityState.Modified; // Явно помечаем профиль как изменённый
            await _context.SaveChangesAsync();
            _logger.LogInformation("Profile updated for user: {UserId}, PortfolioItems count: {PortfolioCount}, AvatarUrl: {AvatarUrl}",
                userId, profile.PortfolioItems?.Count ?? 0, profile.AvatarUrl);

            return profile;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for user: {UserId}", userId);
            throw new Exception("Ошибка при обновлении профиля", ex);
        }
    }

    public async Task<PortfolioItemResponseDto> AddPortfolioItemAsync(Guid userId, AddPortfolioItemDto dto)
    {
        _logger.LogInformation("Adding portfolio item for UserId: {UserId}", userId);
        var profile = await _context.FreelancerProfiles
            .Include(p => p.PortfolioItems)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            _logger.LogWarning("Profile not found for user: {UserId}", userId);
            throw new KeyNotFoundException("Профиль фрилансера не найден");
        }

        var fileUrl = await _fileService.UploadFileAsync(dto.File, "portfolios", userId);
        var portfolioItem = new PortfolioItem
        {
            Id = Guid.NewGuid(),
            FreelancerProfileId = profile.Id,
            Url = fileUrl,
            Description = dto.Description?.Trim() ?? string.Empty
        };

        try
        {
            _context.PortfolioItems.Add(portfolioItem);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Portfolio item added for user: {UserId}, URL: {FileUrl}", userId, fileUrl);

            return new PortfolioItemResponseDto
            {
                Id = portfolioItem.Id,
                Url = portfolioItem.Url,
                Description = portfolioItem.Description
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save portfolio item for user: {UserId}. Inner Exception: {InnerMessage}",
                userId, ex.InnerException?.Message);
            throw new Exception("Ошибка при сохранении портфолио: " + ex.InnerException?.Message, ex);
        }
    }

    public async Task DeletePortfolioItemAsync(Guid userId, Guid portfolioId)
    {
        var profile = await _context.FreelancerProfiles
            .Include(p => p.PortfolioItems)
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            _logger.LogWarning("Profile not found for user: {UserId}", userId);
            throw new KeyNotFoundException("Профиль фрилансера не найден");
        }

        var item = profile.PortfolioItems.FirstOrDefault(p => p.Id == portfolioId);
        if (item == null)
        {
            _logger.LogWarning("Portfolio item not found for user: {UserId}, ID: {PortfolioId}", userId, portfolioId);
            throw new KeyNotFoundException("Работа не найдена в портфолио");
        }

        _context.PortfolioItems.Remove(item);
        await _context.SaveChangesAsync();

        // Удаление файла
        if (!string.IsNullOrEmpty(item.Url))
        {
            var fileId = Path.GetFileNameWithoutExtension(item.Url.Split('/').Last());
            if (Guid.TryParse(fileId, out var guidFileId))
            {
                await _fileService.DeleteFileAsync(guidFileId);
                _logger.LogInformation("Portfolio file deleted for user: {UserId}, FileId: {FileId}", userId, guidFileId);
            }
        }

        _logger.LogInformation("Portfolio item deleted for user: {UserId}, ID: {PortfolioId}", userId, portfolioId);
    }
    public async Task<(List<FreelancerProfile> Freelancers, List<Team> Teams)> SearchFreelancersAsync(SearchFreelancersDto dto)
    {
        var freelancerQuery = _context.FreelancerProfiles
            .Include(p => p.User)
            .Include(p => p.PortfolioItems)
            .AsQueryable();

        var teamQuery = _context.Teams
            .Include(t => t.Leader)
            .AsQueryable();

        if (!string.IsNullOrEmpty(dto.SearchTerm))
        {
            var searchTerms = dto.SearchTerm
                .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim().ToLower())
                .Where(s => !string.IsNullOrWhiteSpace(s) && !s.Any(c => "&|!".Contains(c)) && s.Length >= 2)
                .ToArray();

            if (!searchTerms.Any())
            {
                _logger.LogWarning("Некорректный поисковый запрос: {SearchTerm}", dto.SearchTerm);
                return (new List<FreelancerProfile>(), new List<Team>());
            }

            // Фрилансеры: Поиск с релевантностью
            var allFreelancers = await freelancerQuery.ToListAsync();

            // Фильтрация с релевантностью в памяти
            var freelancerResults = allFreelancers
                .Select(p => new
                {
                    Profile = p,
                    Relevance = searchTerms.Sum(term =>
                        (p.User.Name.ToLower().Contains(term) ? 10 : 0) +
                        (p.User.Email.ToLower().Contains(term) ? 8 : 0) +
                        (p.Bio != null && p.Bio.ToLower().Contains(term) ? 5 : 0) +
                        (p.Skills.Any(s => s.ToLower().Contains(term)) ? 7 : 0) +
                        (p.PortfolioItems.Any(pi => pi.Description != null && pi.Description.ToLower().Contains(term)) ? 6 : 0) +
                        (dto.UseFuzzySearch ?
                            (CalculateLevenshteinSimilarity(p.User.Name.ToLower(), term) > 0.7 ? 4 : 0) +
                            (p.Skills.Any(s => CalculateLevenshteinSimilarity(s.ToLower(), term) > 0.7) ? 3 : 0)
                            : 0))
            
                })
                .Where(x => x.Relevance > 0)
                .OrderByDescending(x => x.Relevance)
                .ThenByDescending(x => x.Profile.Rating)
                .ThenByDescending(x => x.Profile.PortfolioItems.Count)
                .ThenByDescending(x => x.Profile.Skills.Count)
                .ThenByDescending(x => x.Profile.Bio != null && x.Profile.Bio.Length > 0)
                .ThenBy(x => x.Profile.User.Name)
                .Skip((dto.Page - 1) * dto.PageSize)
                .Take(dto.PageSize)
                .Select(x => x.Profile)
                .ToList();

            // Команды: Поиск в памяти с релевантностью
            var allTeams = await teamQuery.ToListAsync();
            var filteredTeams = allTeams
                .Select(t =>
                {
                    var portfolioItems = new List<PortfolioItemDto>();
                    if (t.Portfolio != null)
                    {
                        try
                        {
                            portfolioItems = JsonSerializer.Deserialize<List<PortfolioItemDto>>(
                                t.Portfolio.RootElement.GetRawText(),
                                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
                            ) ?? new List<PortfolioItemDto>();
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize portfolio for team {TeamId}", t.Id);
                        }
                    }

                    var relevance = searchTerms.Sum(term =>
                        (t.Name?.ToLower().Contains(term) ?? false ? 10 : 0) + // Вес для имени команды
                        (t.Leader?.Name.ToLower().Contains(term) ?? false ? 9 : 0) + // Вес для имени лидера
                        (t.Leader?.Email.ToLower().Contains(term) ?? false ? 8 : 0) + // Вес для почты лидера
                        (t.Skills?.Any(s => s != null && s.ToLower().Contains(term)) ?? false ? 7 : 0) + // Вес для скиллов
                        (portfolioItems.Any(p => (p.Title?.ToLower().Contains(term) ?? false) ||
                                                (p.Description?.ToLower().Contains(term) ?? false)) ? 6 : 0) + // Вес для портфолио
                                                                                                               // Нечёткий поиск
                        (dto.UseFuzzySearch ?
                            (CalculateLevenshteinSimilarity(t.Name?.ToLower() ?? "", term) > 0.7 ? 4 : 0) +
                            (t.Skills?.Any(s => s != null && CalculateLevenshteinSimilarity(s.ToLower(), term) > 0.7) ?? false ? 3 : 0) +
                            (portfolioItems.Any(p => (p.Title != null && CalculateLevenshteinSimilarity(p.Title.ToLower(), term) > 0.7) ||
                                                    (p.Description != null && CalculateLevenshteinSimilarity(p.Description.ToLower(), term) > 0.7)) ? 2 : 0)
                            : 0)
                    );

                    return new { Team = t, Relevance = relevance };
                })
                .Where(x => x.Relevance > 0)
                .OrderByDescending(x => x.Relevance)
                .ThenByDescending(x => x.Team.Rating)
                .ThenByDescending(x => x.Team.Name)
                .Skip((dto.Page - 1) * dto.PageSize)
                .Take(dto.PageSize)
                .Select(x => x.Team)
                .ToList();

            if (dto.MinRating.HasValue)
            {
                freelancerResults = freelancerResults.Where(p => p.Rating >= dto.MinRating.Value).ToList();
                filteredTeams = filteredTeams.Where(t => t.Rating >= dto.MinRating.Value).ToList();
            }

            if (!string.IsNullOrEmpty(dto.Level) && Enum.TryParse<FreelancerLevel>(dto.Level, out var level))
            {
                freelancerResults = freelancerResults.Where(p => p.Level == level).ToList();
            }
            else if (!string.IsNullOrEmpty(dto.Level))
            {
                _logger.LogWarning("Некорректный формат уровня: {Level}", dto.Level);
            }

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                _logger.LogInformation("Загружено {FreelancerCount} фрилансеров и {TeamCount} команд за {ElapsedMilliseconds}ms",
                    freelancerResults.Count, filteredTeams.Count, stopwatch.ElapsedMilliseconds);

                return (freelancerResults, filteredTeams);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при загрузке фрилансеров или команд");
                throw new Exception("Ошибка при поиске фрилансеров или команд", ex);
            }
        }

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            var freelancerResults = await freelancerQuery
                .OrderByDescending(p => p.Rating)
                .ThenByDescending(p => p.PortfolioItems.Count)
                .ThenByDescending(p => p.Skills.Count)
                .ThenByDescending(p => p.Bio != null && p.Bio.Length > 0)
                .ThenBy(p => p.User.Name)
                .Skip((dto.Page - 1) * dto.PageSize)
                .Take(dto.PageSize)
                .ToListAsync();

            var teamResults = await teamQuery
                .OrderByDescending(t => t.Rating)
                .ThenByDescending(t => t.Name)
                .Skip((dto.Page - 1) * dto.PageSize)
                .Take(dto.PageSize)
                .ToListAsync();

            if (dto.MinRating.HasValue)
            {
                freelancerResults = freelancerResults.Where(p => p.Rating >= dto.MinRating.Value).ToList();
                teamResults = teamResults.Where(t => t.Rating >= dto.MinRating.Value).ToList();
            }

            if (!string.IsNullOrEmpty(dto.Level) && Enum.TryParse<FreelancerLevel>(dto.Level, out var level))
            {
                freelancerResults = freelancerResults.Where(p => p.Level == level).ToList();
            }
            else if (!string.IsNullOrEmpty(dto.Level))
            {
                _logger.LogWarning("Некорректный формат уровня: {Level}", dto.Level);
            }

            stopwatch.Stop();
            _logger.LogInformation("Загружено {FreelancerCount} фрилансеров и {TeamCount} команд за {ElapsedMilliseconds}ms",
                freelancerResults.Count, teamResults.Count, stopwatch.ElapsedMilliseconds);

            return (freelancerResults, teamResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке фрилансеров или команд");
            throw new Exception("Ошибка при поиске фрилансеров или команд", ex);
        }
    }
    private double CalculateLevenshteinSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0.0;
        int maxLen = Math.Max(source.Length, target.Length);
        if (maxLen == 0) return 1.0;

        int distance = ComputeLevenshteinDistance(source, target);
        return 1.0 - (double)distance / maxLen;
    }

    private int ComputeLevenshteinDistance(string source, string target)
    {
        int[,] matrix = new int[source.Length + 1, target.Length + 1];

        for (int i = 0; i <= source.Length; i++)
            matrix[i, 0] = i;
        for (int j = 0; j <= target.Length; j++)
            matrix[0, j] = j;

        for (int i = 1; i <= source.Length; i++)
        {
            for (int j = 1; j <= target.Length; j++)
            {
                int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[source.Length, target.Length];
    }

    public async Task<List<KeyValuePair<string, int>>> GetPopularSkillsAsync(string prefix = "")
    {
        var profiles = await _context.FreelancerProfiles
            .Where(p => p.Skills != null && p.Skills.Any())
            .Select(p => p.Skills)
            .ToListAsync();

        var skillCounts = new Dictionary<string, int>();
        foreach (var skillList in profiles)
        {
            foreach (var skill in skillList)
            {
                if (!string.IsNullOrWhiteSpace(skill))
                {
                    var normalizedSkill = skill.Trim().ToLower();
                    skillCounts[normalizedSkill] = skillCounts.GetValueOrDefault(normalizedSkill, 0) + 1;
                }
            }
        }

        var filteredSkills = skillCounts
            .Where(kv => string.IsNullOrEmpty(prefix) || kv.Key.StartsWith(prefix.Trim().ToLower()))
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .ToList();

        _logger.LogInformation("Retrieved {Count} popular skills for prefix '{Prefix}'", filteredSkills.Count, prefix);
        return filteredSkills;
    }

    public async Task<AnalyticsResponseDto> GetProfileAnalyticsAsync(Guid userId, DateTime? startDate, DateTime? endDate)
    {
        _logger.LogInformation("GetProfileAnalyticsAsync started for UserId: {UserId}", userId);

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
        {
            _logger.LogWarning("User not found for UserId: {UserId}", userId);
            throw new KeyNotFoundException("Пользователь не найден");
        }

        // Определяем временной диапазон
        var start = startDate ?? DateTime.UtcNow.AddDays(-30);
        var end = endDate ?? DateTime.UtcNow;
        _logger.LogInformation("Date range: Start={Start}, End={End}", start, end);

        // Получаем последние заказы с учетом анонимности
        var ordersQuery = _context.Orders
            .Where(o => (o.FreelancerId == userId || o.ClientId == userId) && o.CreatedAt >= start && o.CreatedAt <= end)
            .OrderByDescending(o => o.CreatedAt)
            .Take(5);

        var recentOrders = await ordersQuery
            .Select(o => new OrderResponseDto
            {
                Id = o.Id,
                Title = o.Title,
                Description = o.Description,
                Budget = o.Budget,
                Type = o.Type,
                IsTurbo = o.IsTurbo,
                IsAnonymous = o.IsAnonymous,
                Deadline = o.Deadline,
                Status = o.Status,
                ClientId = o.ClientId,
                Client = o.IsAnonymous ? new ClientDto
                {
                    Email = null,
                    Name = "Анонимный клиент",
                    AvatarUrl = null
                } : o.Client != null ? new ClientDto
                {
                    Id = o.Client.Id,
                    Email = o.Client.Email,
                    Name = o.Client.Name,
                    AvatarUrl = _context.ClientProfiles
                        .Where(cp => cp.UserId == o.ClientId)
                        .Select(cp => cp.AvatarUrl)
                        .FirstOrDefault()
                } : null,
                FreelancerId = o.FreelancerId,
                CreatedAt = o.CreatedAt,
                Invoices = o.Invoices.Select(i => new InvoiceResponseDto
                {
                    Id = i.Id,
                    OrderId = i.OrderId,
                    Amount = i.Amount,
                    Status = i.Status,
                    CreatedAt = i.CreatedAt
                }).ToList()
            })
            .ToListAsync();

        // Логирование заказов
        foreach (var order in recentOrders)
        {
            _logger.LogInformation(
                "Order {OrderId}, IsAnonymous: {IsAnonymous}, ClientName: {ClientName}, ClientEmail: {ClientEmail}, ClientAvatar: {ClientAvatar}, ClientId: {ClientId}",
                order.Id, order.IsAnonymous, order.Client?.Name ?? "null", order.Client?.Email ?? "null", order.Client?.AvatarUrl ?? "null", order.Client?.Id?.ToString() ?? "null");
        }

        // Принудительная анонимизация
        foreach (var order in recentOrders)
        {
            if (order.IsAnonymous && order.Client != null)
            {
                if (order.Client.Id != null || order.Client.Name != "Анонимный клиент" || order.Client.Email != null || order.Client.AvatarUrl != null)
                {
                    _logger.LogWarning(
                        "Forced anonymization for Order {OrderId}, original ClientId: {ClientId}, ClientName: {ClientName}, ClientEmail: {ClientEmail}, ClientAvatar: {ClientAvatar}",
                        order.Id, order.Client.Id, order.Client.Name, order.Client.Email, order.Client.AvatarUrl);
                    order.Client = new ClientDto
                    {
                        Email = null,
                        Name = "Анонимный клиент",
                        AvatarUrl = null
                    };
                }
            }
        }

        // Подсчет статистики
        var totalOrders = await _context.Orders
            .CountAsync(o => o.FreelancerId == userId || o.ClientId == userId);

        var completedOrders = await _context.Orders
            .CountAsync(o => (o.FreelancerId == userId || o.ClientId == userId) && o.Status == "Completed");

        var totalEarnings = await _context.Payments
            .Where(p => p.Order.FreelancerId == userId && p.Status == "Paid")
            .SumAsync(p => p.Amount);

        var averageRating = await _context.Reviews
            .Where(r => r.Order.FreelancerId == userId)
            .AverageAsync(r => (decimal?)r.Rating) ?? 0;

        var activeOrders = await _context.Orders
            .CountAsync(o => (o.FreelancerId == userId || o.ClientId == userId) && o.Status == "Active");

        _logger.LogInformation(
            "Analytics retrieved for UserId: {UserId}, TotalOrders: {TotalOrders}, CompletedOrders: {CompletedOrders}, TotalEarnings: {TotalEarnings}, AverageRating: {AverageRating}, ActiveOrders: {ActiveOrders}",
            userId, totalOrders, completedOrders, totalEarnings, averageRating, activeOrders);

        return new AnalyticsResponseDto
        {
            RecentOrders = recentOrders,
            Statistics = new UserStatsDto
            {
                TotalOrders = totalOrders,
                CompletedOrders = completedOrders,
                TotalEarnings = totalEarnings,
                AverageRating = Math.Round(averageRating, 2),
                ActiveOrders = activeOrders
            }
        };
    }
}