using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class ReviewService
{
    private readonly AppDbContext _context;

    public ReviewService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Review> CreateReviewAsync(Guid userId, CreateReviewDto dto)
    {
        var order = await _context.Orders.FindAsync(dto.OrderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId && order.FreelancerId != userId)
            throw new UnauthorizedAccessException("Только участники заказа могут оставить отзыв");

        var review = new Review
        {
            Id = Guid.NewGuid(),
            OrderId = dto.OrderId,
            ReviewerId = userId,
            Rating = dto.Rating,
            Comment = dto.Comment,
            IsAnonymous = dto.IsAnonymous,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reviews.Add(review);
        await _context.SaveChangesAsync();

        return review;
    }

    public async Task<List<Review>> GetReviewsAsync(Guid userId, int page, int pageSize)
    {
        return await _context.Reviews
            .Where(r => r.ReviewerId == userId || r.Order.FreelancerId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Include(r => r.Order)
            .ToListAsync();
    }

    public async Task DisputeReviewAsync(Guid reviewId, Guid userId, DisputeReviewDto dto)
    {
        var review = await _context.Reviews.FindAsync(reviewId)
            ?? throw new KeyNotFoundException("Отзыв не найден");

        var order = await _context.Orders.FindAsync(review.OrderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId && order.FreelancerId != userId)
            throw new UnauthorizedAccessException("Только участники заказа могут оспаривать отзыв");

        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = userId,
            TargetId = reviewId,
            TargetType = "Review",
            Reason = dto.Reason,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();
    }
    public async Task<List<ReviewResponseDto>> GetFreelancerReviewsAsync(Guid freelancerId, int page, int pageSize)
    {
        return await _context.Reviews
            .Include(r => r.Reviewer)
            .Include(r => r.Order)
            .Where(r => r.Order.FreelancerId == freelancerId)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ReviewResponseDto
            {
                Id = r.Id,
                OrderId = r.OrderId,
                ReviewerId = r.IsAnonymous ? null : r.ReviewerId,
                ReviewerName = r.IsAnonymous ? "Аноним" : (r.Reviewer != null ? r.Reviewer.Name : "Неизвестный пользователь"),
                Rating = r.Rating,
                Comment = r.Comment,
                IsAnonymous = r.IsAnonymous,
                CreatedAt = r.CreatedAt
            })
            .ToListAsync();
    }
}