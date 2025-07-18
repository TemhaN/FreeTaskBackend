using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services
{
    public class FreelancerLevelService
    {
        private readonly AppDbContext _dbContext;

        public FreelancerLevelService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Пересчитывает очки и уровень для фрилансера
        public async Task UpdateFreelancerLevelAsync(Guid freelancerId)
        {
            // Находим профиль фрилансера
            var freelancerProfile = await _dbContext.FreelancerProfiles
                .FirstOrDefaultAsync(fp => fp.UserId == freelancerId);

            if (freelancerProfile == null)
            {
                throw new Exception("Профиль фрилансера не найден.");
            }

            // Подсчет очков
            int totalPoints = await CalculateLevelPointsAsync(freelancerId);

            // Обновляем очки
            freelancerProfile.LevelPoints = totalPoints;

            // Обновляем уровень на основе очков
            freelancerProfile.Level = DetermineLevel(totalPoints);

            // Сохраняем изменения в базе данных
            await _dbContext.SaveChangesAsync();
        }

        // Вычисляет общее количество очков для фрилансера
        private async Task<int> CalculateLevelPointsAsync(Guid freelancerId)
        {
            int totalPoints = 0;

            // Очки за завершенные заказы
            var completedOrders = await _dbContext.Orders
                .Where(o => o.FreelancerId == freelancerId && o.Status == "Completed")
                .ToListAsync();

            foreach (var order in completedOrders)
            {
                totalPoints += order.IsTurbo ? 15 : 10; // Турбо-заказы дают +15, обычные +10
            }

            // Очки за отзывы
            var reviews = await _dbContext.Reviews
                .Where(r => r.Order.FreelancerId == freelancerId)
                .ToListAsync();

            foreach (var review in reviews)
            {
                totalPoints += review.Rating switch
                {
                    5 => 5,
                    4 => 3,
                    _ => 1
                };
            }

            return totalPoints;
        }

        // Определяет уровень на основе очков
        private FreelancerLevel DetermineLevel(int points)
        {
            if (points >= 501)
                return FreelancerLevel.Expert;
            if (points >= 101)
                return FreelancerLevel.Specialist;
            return FreelancerLevel.Newbie;
        }

        // Вызывается при создании или обновлении заказа
        public async Task HandleOrderUpdateAsync(Guid orderId)
        {
            var order = await _dbContext.Orders
                .Include(o => o.Freelancer)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order?.FreelancerId != null && order.Status == "Completed")
            {
                await UpdateFreelancerLevelAsync(order.FreelancerId.Value);
            }
        }

        // Вызывается при создании отзыва
        public async Task HandleReviewCreatedAsync(Guid reviewId)
        {
            var review = await _dbContext.Reviews
                .Include(r => r.Order)
                .ThenInclude(o => o.Freelancer)
                .FirstOrDefaultAsync(r => r.Id == reviewId);

            if (review?.Order?.FreelancerId != null)
            {
                await UpdateFreelancerLevelAsync(review.Order.FreelancerId.Value);
            }
        }
    }
}