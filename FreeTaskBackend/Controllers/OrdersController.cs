using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/orders")]
public class OrdersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly OrderService _orderService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(AppDbContext context, OrderService orderService, ILogger<OrdersController> logger)
    {
        _context = context;
        _orderService = orderService;
        _logger = logger;
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Client")
            return Forbid();

        try
        {
            var order = await _orderService.CreateOrderAsync(userId, dto);
            _logger.LogInformation("Created order {OrderId} for client {ClientId}", order.Id, userId);

            var client = await _context.Users
                .Where(u => u.Id == order.ClientId)
                .Select(u => new ClientDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Name = u.Name,
                    AvatarUrl = u.VerificationDocumentUrl
                })
                .FirstOrDefaultAsync();

            return Ok(new OrderResponseDto
            {
                Id = order.Id,
                Title = order.Title,
                Description = order.Description,
                Budget = order.Budget,
                Type = order.Type,
                IsTurbo = order.IsTurbo,
                IsAnonymous = order.IsAnonymous,
                Deadline = order.Deadline,
                Status = order.Status,
                ClientId = order.IsAnonymous ? null : order.ClientId,
                Client = order.IsAnonymous ? null : client,
                FreelancerId = order.FreelancerId,
                CreatedAt = order.CreatedAt,
                Invoices = order.Invoices?.Select(i => new InvoiceResponseDto
                {
                    Id = i.Id,
                    OrderId = i.OrderId,
                    Amount = i.Amount,
                    Status = i.Status,
                    CreatedAt = i.CreatedAt
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for user: {UserId}. Inner Exception: {InnerException}", userId, ex.InnerException?.Message);
            return BadRequest(new { Message = ex.Message, InnerException = ex.InnerException?.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetOrders([FromQuery] OrderFilterDto dto)
    {
        try
        {
            var orders = await _orderService.GetOrdersAsync(dto);
            var response = await Task.WhenAll(orders.Select(async o =>
            {
                var client = o.IsAnonymous ? null : await _context.Users
                    .Where(u => u.Id == o.ClientId)
                    .Select(u => new ClientDto
                    {
                        Id = u.Id,
                        Email = u.Email,
                        Name = u.Name,
                        AvatarUrl = u.VerificationDocumentUrl
                    })
                    .FirstOrDefaultAsync();

                return new OrderResponseDto
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
                    ClientId = o.IsAnonymous ? null : o.ClientId,
                    Client = client,
                    FreelancerId = o.FreelancerId,
                    CreatedAt = o.CreatedAt,
                    Invoices = o.Invoices?.Select(i => new InvoiceResponseDto
                    {
                        Id = i.Id,
                        OrderId = i.OrderId,
                        Amount = i.Amount,
                        Status = i.Status,
                        CreatedAt = i.CreatedAt
                    }).ToList()
                };
            }));

            _logger.LogInformation("Retrieved {OrderCount} orders", response.Length);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders");
            return BadRequest(new { ex.Message });
        }
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrder(Guid id)
    {
        try
        {
            var order = await _orderService.GetOrderAsync(id);

            var client = order.IsAnonymous ? null : await _context.Users
                .Where(u => u.Id == order.ClientId)
                .Select(u => new ClientDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Name = u.Name,
                    AvatarUrl = u.VerificationDocumentUrl
                })
                .FirstOrDefaultAsync();

            var response = new OrderResponseDto
            {
                Id = order.Id,
                Title = order.Title,
                Description = order.Description,
                Budget = order.Budget,
                Type = order.Type,
                IsTurbo = order.IsTurbo,
                IsAnonymous = order.IsAnonymous,
                Deadline = order.Deadline,
                Status = order.Status,
                ClientId = order.IsAnonymous ? null : order.ClientId,
                Client = client,
                FreelancerId = order.FreelancerId,
                CreatedAt = order.CreatedAt,
                Invoices = order.Invoices?.Select(i => new InvoiceResponseDto
                {
                    Id = i.Id,
                    OrderId = i.OrderId,
                    Amount = i.Amount,
                    Status = i.Status,
                    CreatedAt = i.CreatedAt
                }).ToList()
            };

            _logger.LogInformation("Retrieved order {OrderId} with ClientId {ClientId}, FreelancerId {FreelancerId}", id, order.ClientId, order.FreelancerId);
            return Ok(response);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { ex.Message });
        }
    }

    [Authorize]
    [HttpPost("{id}/bids")]
    public async Task<IActionResult> PlaceBid(Guid id, [FromBody] PlaceBidDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Freelancer")
            return Forbid();
        var order = await _orderService.GetOrderAsync(id);
        if (order.Status != "Open")
            return BadRequest("Order is not open for bids.");

        try
        {
            var bid = await _orderService.PlaceBidAsync(id, userId, dto);
            _logger.LogInformation("Placed bid for order {OrderId} by freelancer {FreelancerId}", id, userId);
            return Ok(bid);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Order {OrderId} not found for bid", id);
            return NotFound(new { ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id}/status")]
    public async Task<IActionResult> UpdateOrderStatus(Guid id, [FromBody] UpdateOrderStatusDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
                return NotFound(new { message = "Заказ не найден" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                return Unauthorized(new { message = "Пользователь не найден" });

            _logger.LogInformation(
                "UpdateOrderStatus attempt: OrderId={OrderId}, UserId={UserId}, Role={Role}, Status={Status}, OrderFreelancerId={FreelancerId}",
                id, userId, user.Role, dto.Status, order.FreelancerId);

            await _orderService.UpdateOrderStatusAsync(id, userId, dto);
            _logger.LogInformation("Updated status for order {OrderId} to {Status}", id, dto.Status);
            return Ok(new { message = "Статус заказа обновлен" });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Unauthorized attempt to update order {OrderId} by user {UserId}: {Message}", id, userId, ex.Message);
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order status: {OrderId}. Message: {Message}", id, ex.Message);
            return BadRequest(new { message = "Ошибка при обновлении статуса заказа" });
        }
    }

    [Authorize]
    [HttpPost("{id}/extend-deadline")]
    public async Task<IActionResult> ExtendDeadline(Guid id, [FromBody] ExtendDeadlineDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            await _orderService.ExtendDeadlineAsync(id, userId, dto);
            _logger.LogInformation("Extended deadline for order {OrderId}", id);
            return Ok(new { message = "Дедлайн продлен" });
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Unauthorized attempt to extend deadline for order {OrderId} by user {UserId}", id, userId);
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending deadline for order: {OrderId}", id);
            return BadRequest(new { message = "Ошибка при продлении дедлайна" });
        }
    }

    [Authorize]
    [HttpPost("{id}/accept")]
    public async Task<IActionResult> AcceptOrder(Guid id, [FromBody] AcceptOrderDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Freelancer")
            return Forbid();

        try
        {
            await _orderService.AcceptOrderAsync(id, userId, dto);
            _logger.LogInformation("Order {OrderId} {Action} by freelancer {FreelancerId}", id, dto.Accept ? "accepted" : "rejected", userId);

            var order = await _orderService.GetOrderAsync(id);
            var client = order.IsAnonymous ? null : await _context.Users
                .Where(u => u.Id == order.ClientId)
                .Select(u => new ClientDto
                {
                    Id = u.Id,
                    Email = u.Email,
                    Name = u.Name,
                    AvatarUrl = u.VerificationDocumentUrl
                })
                .FirstOrDefaultAsync();

            return Ok(new OrderResponseDto
            {
                Id = order.Id,
                Title = order.Title,
                Description = order.Description,
                Budget = order.Budget,
                Type = order.Type,
                IsTurbo = order.IsTurbo,
                IsAnonymous = order.IsAnonymous,
                Deadline = order.Deadline,
                Status = order.Status,
                ClientId = order.IsAnonymous ? null : order.ClientId,
                Client = client,
                FreelancerId = order.FreelancerId,
                CreatedAt = order.CreatedAt,
                Invoices = order.Invoices?.Select(i => new InvoiceResponseDto
                {
                    Id = i.Id,
                    OrderId = i.OrderId,
                    Amount = i.Amount,
                    Status = i.Status,
                    CreatedAt = i.CreatedAt
                }).ToList()
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId} by freelancer {FreelancerId}", id, userId);
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("{id}/invoice")]
    public async Task<IActionResult> CreateInvoice(Guid id, [FromBody] CreateInvoiceDto dto)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Freelancer")
            return Forbid();

        try
        {
            var invoice = await _orderService.CreateInvoiceAsync(id, userId, dto);
            _logger.LogInformation("Invoice created for order {OrderId} by freelancer {FreelancerId}", id, userId);
            return Ok(new InvoiceResponseDto
            {
                Id = invoice.Id,
                OrderId = invoice.OrderId,
                Amount = invoice.Amount,
                Status = invoice.Status,
                CreatedAt = invoice.CreatedAt
            });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Invalid operation for order {OrderId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning("Invalid argument for order {OrderId}: {Message}", id, ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invoice for order {OrderId}", id);
            return BadRequest(new { message = "Ошибка при создании счета" });
        }
    }

    [Authorize]
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> CompleteOrder(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Freelancer")
            return Forbid();

        try
        {
            await _orderService.CompleteOrderAsync(id, userId);
            _logger.LogInformation("Order {OrderId} marked as completed by freelancer {FreelancerId}", id, userId);
            return Ok(new { message = "Заказ завершен фрилансером" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing order {OrderId}", id);
            return BadRequest(new { message = ex.Message });
        }
    }
    [Authorize]
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelOrder(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Client")
            return Forbid();

        try
        {
            await _orderService.CancelOrderAsync(id, userId);
            _logger.LogInformation("Order {OrderId} cancelled by client {ClientId}", id, userId);
            return Ok(new { message = "Заказ отменён" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException)
        {
            _logger.LogWarning("Unauthorized attempt to cancel order {OrderId} by user {UserId}", id, userId);
            return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", id);
            return BadRequest(new { message = ex.Message });
        }
    }
    [Authorize]
    [HttpPost("{id}/confirm")]
    public async Task<IActionResult> ConfirmOrder(Guid id)
    {
        var userId = Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user?.Role != "Client")
            return Forbid();

        try
        {
            await _orderService.ConfirmOrderAsync(id, userId);
            _logger.LogInformation("Order {OrderId} confirmed by client {ClientId}", id, userId);
            return Ok(new { message = "Заказ подтвержден клиентом" });
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning("Order {OrderId} not found", id);
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming order {OrderId}", id);
            return BadRequest(new { message = ex.Message });
        }
    }
}