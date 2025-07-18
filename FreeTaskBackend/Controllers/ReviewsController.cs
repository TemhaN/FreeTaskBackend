using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Climate;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly ReviewService _reviewService;
    private readonly FreeTaskBackend.Services.OrderService _orderService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(ReviewService reviewService, FreeTaskBackend.Services.OrderService orderService, ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _orderService = orderService;
        _logger = logger;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        var order = await _orderService.GetOrderAsync(dto.OrderId);
        if (order.Status != "Completed")
            return BadRequest("Order is not completed.");

        try
        {

            var review = await _reviewService.CreateReviewAsync(userId, dto);

            return Ok(new ReviewResponseDto
            {
                Id = review.Id,
                OrderId = review.OrderId,
                ReviewerId = review.IsAnonymous ? null : review.ReviewerId,
                Rating = review.Rating,
                Comment = review.Comment,
                IsAnonymous = review.IsAnonymous,
                CreatedAt = review.CreatedAt
            });

        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating review for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при создании отзыва" });
        }
    }

    [HttpGet("{userId}")]
    public async Task<IActionResult> GetReviews(Guid userId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var reviews = await _reviewService.GetReviewsAsync(userId, page, pageSize);
            var response = reviews.Select(r => new ReviewResponseDto
            {
                Id = r.Id,
                OrderId = r.OrderId,
                ReviewerId = r.IsAnonymous ? null : r.ReviewerId,
                Rating = r.Rating,
                Comment = r.Comment,
                IsAnonymous = r.IsAnonymous,
                CreatedAt = r.CreatedAt
            }).ToList();
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving reviews for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при получении отзывов" });
        }
    }

    [Authorize]
    [HttpPut("{id}/dispute")]
    public async Task<IActionResult> DisputeReview(Guid id, [FromBody] DisputeReviewDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            await _reviewService.DisputeReviewAsync(id, userId, dto);
            return Ok(new { message = "Отзыв оспорен" });
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disputing review: {ReviewId}", id);
            return BadRequest(new { message = "Ошибка при оспаривании отзыва" });
        }
    }
}