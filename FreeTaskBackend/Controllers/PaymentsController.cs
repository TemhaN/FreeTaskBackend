using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System;
using System.Threading.Tasks;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private readonly PaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;
    private readonly AppDbContext _context;
    public PaymentsController(
        AppDbContext context,
        PaymentService paymentService,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _paymentService = paymentService;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("test")]
    public async Task<IActionResult> CreateTestPayment([FromBody] CreateTestPaymentDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var payment = await _paymentService.CreateTestPaymentAsync(userId, dto);
            return Ok(new PaymentResponseDto
            {
                Id = payment.Id,
                OrderId = payment.OrderId,
                Amount = payment.Amount,
                Status = payment.Status,
                StripePaymentId = payment.StripePaymentId,
                IsTest = payment.IsTest,
                CreatedAt = payment.CreatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test payment for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при создании тестового платежа" });
        }
    }

    [Authorize]
    [HttpPost("{id}/release")]
    public async Task<IActionResult> ReleasePayment(Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            await _paymentService.ReleasePaymentAsync(id, userId);
            return Ok(new { message = "Средства выпущены" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error releasing payment: {PaymentId}", id);
            return BadRequest(new { message = "Ошибка при выпуске средств" });
        }
    }

    [Authorize]
    [HttpPost("{id}/refund")]
    public async Task<IActionResult> RefundPayment(Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            await _paymentService.RefundPaymentAsync(id, userId);
            return Ok(new { message = "Средства возвращены" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refunding payment: {PaymentId}", id);
            return BadRequest(new { message = "Ошибка при возврате средств" });
        }
    }

    [Authorize]
    [HttpGet("{id}/status")]
    public async Task<IActionResult> GetPaymentStatus(Guid id)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var payment = await _paymentService.GetPaymentStatusAsync(id, userId);
            return Ok(new PaymentResponseDto
            {
                Id = payment.Id,
                OrderId = payment.OrderId,
                Amount = payment.Amount,
                Status = payment.Status,
                StripePaymentId = payment.StripePaymentId,
                IsTest = payment.IsTest,
                CreatedAt = payment.CreatedAt
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving payment status: {PaymentId}", id);
            return BadRequest(new { message = "Ошибка при получении статуса платежа" });
        }
    }

    [Authorize]
    [HttpPost("{id}/dispute")]
    public async Task<IActionResult> OpenDispute(Guid id, [FromBody] OpenDisputeDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            await _paymentService.OpenDisputeAsync(id, userId, dto);
            return Ok(new { message = "Спор открыт" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening dispute for payment: {PaymentId}", id);
            return BadRequest(new { message = "Ошибка при открытии спора" });
        }
    }
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentDto dto)
    {
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var payment = await _paymentService.CreatePaymentAsync(userId, dto);
            return Ok(new PaymentResponseDto
            {
                Id = payment.Id,
                OrderId = payment.OrderId,
                Amount = payment.Amount,
                Status = payment.Status,
                StripePaymentId = payment.StripePaymentId,
                ClientSecret = payment.StripePaymentId.StartsWith("pi_") ? (await new PaymentIntentService().GetAsync(payment.StripePaymentId)).ClientSecret : null,
                IsTest = payment.IsTest,
                CreatedAt = payment.CreatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при создании платежа" });
        }
    }
    [HttpPost("webhook")]
    public async Task<IActionResult> HandleWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var stripeEvent = EventUtility.ParseEvent(json);

        if (stripeEvent.Type == Events.PaymentIntentSucceeded)
        {
            var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.StripePaymentId == paymentIntent.Id);
            if (payment != null)
            {
                payment.Status = "Paid";
                var invoice = await _context.Invoices.FindAsync(payment.InvoiceId);
                if (invoice != null)
                {
                    invoice.Status = "Paid";
                }
                await _context.SaveChangesAsync();
                _logger.LogInformation("Webhook: Payment {PaymentId} marked as Paid", payment.Id);
            }
        }

        return Ok();
    }
}