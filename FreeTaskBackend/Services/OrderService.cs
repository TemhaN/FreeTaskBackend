using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class OrderService
{
    private readonly AppDbContext _context;

    public OrderService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Order> CreateOrderAsync(Guid userId, CreateOrderDto dto)
    {
        var team = dto.TeamId.HasValue ? await _context.Teams.FindAsync(dto.TeamId) : null;
        if (dto.TeamId.HasValue && team == null)
            throw new KeyNotFoundException("Команда не найдена");

        if (dto.IsTurbo)
        {
            if (!dto.Deadline.HasValue)
                throw new ArgumentException("Для турбо-заказа необходимо указать дедлайн");

            var now = DateTime.UtcNow;
            var maxDeadline = now.AddHours(48);
            if (dto.Deadline.Value > maxDeadline)
                throw new ArgumentException("Дедлайн турбо-заказа не может превышать 48 часов от текущего времени");

            dto.Deadline = DateTime.SpecifyKind(dto.Deadline.Value, DateTimeKind.Utc);
        }

        var order = new Order
        {
            Id = Guid.NewGuid(),
            ClientId = userId,
            TeamId = dto.TeamId,
            FreelancerId = dto.FreelancerId,
            Title = dto.Title,
            Description = dto.Description,
            Budget = dto.Budget,
            Type = dto.Type,
            IsTurbo = dto.IsTurbo,
            IsAnonymous = dto.IsAnonymous,
            Deadline = dto.Deadline,
            Status = "Open",
            CreatedAt = DateTime.UtcNow
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<List<Order>> GetOrdersAsync(OrderFilterDto dto)
    {
        var query = _context.Orders.AsQueryable();

        if (!string.IsNullOrEmpty(dto.Type))
            query = query.Where(o => o.Type == dto.Type);
        if (!string.IsNullOrEmpty(dto.Status))
            query = query.Where(o => o.Status == dto.Status);

        query = query.OrderByDescending(o => o.IsTurbo).ThenByDescending(o => o.CreatedAt);

        return await query.ToListAsync();
    }

    public async Task<Order> GetOrderAsync(Guid id)
    {
        var order = await _context.Orders
            .Include(o => o.Invoices) // Включаем счета
            .FirstOrDefaultAsync(o => o.Id == id)
            ?? throw new KeyNotFoundException("Заказ не найден");
        return order;
    }

    public async Task<Bid> PlaceBidAsync(Guid orderId, Guid userId, PlaceBidDto dto)
    {
        var order = await _context.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        var bid = new Bid
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            FreelancerId = userId,
            Amount = dto.Amount,
            Comment = dto.Comment,
            CreatedAt = DateTime.UtcNow
        };
        _context.Bids.Add(bid);
        await _context.SaveChangesAsync();
        return bid;
    }

    public async Task UpdateOrderStatusAsync(Guid orderId, Guid userId, UpdateOrderStatusDto dto)
    {
        var order = await _context.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId)
            throw new UnauthorizedAccessException("Только заказчик может изменить статус");

        if (dto.Status == "Completed" && order.IsTurbo && order.FreelancerId.HasValue)
        {
            var freelancerProfile = await _context.FreelancerProfiles
                .FirstOrDefaultAsync(fp => fp.Id == order.FreelancerId.Value);
            if (freelancerProfile != null)
            {
                freelancerProfile.LevelPoints += 1;
                freelancerProfile.Level = freelancerProfile.LevelPoints switch
                {
                    >= 21 => FreelancerLevel.Expert,
                    >= 6 => FreelancerLevel.Specialist,
                    _ => FreelancerLevel.Newbie
                };
            }
        }

        order.Status = dto.Status;
        await _context.SaveChangesAsync();
    }

    public async Task ExtendDeadlineAsync(Guid orderId, Guid userId, ExtendDeadlineDto dto)
    {
        var order = await _context.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId)
            throw new UnauthorizedAccessException("Только заказчик может продлить дедлайн");

        if (order.IsTurbo)
        {
            var maxDeadline = order.CreatedAt.AddHours(48);
            if (dto.NewDeadline > maxDeadline)
                throw new ArgumentException("Дедлайн турбо-заказа не может превышать 48 часов от времени создания");
        }

        order.Deadline = DateTime.SpecifyKind(dto.NewDeadline, DateTimeKind.Utc);
        await _context.SaveChangesAsync();
    }

    public async Task AcceptOrderAsync(Guid orderId, Guid freelancerId, AcceptOrderDto dto)
    {
        var order = await _context.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (dto.Accept)
        {
            order.FreelancerId = freelancerId;
            order.Status = "InProgress";
        }
        else
        {
            order.Status = "Cancelled";
        }

        await _context.SaveChangesAsync();
    }

    public async Task<FreeTaskBackend.Models.Invoice> CreateInvoiceAsync(Guid orderId, Guid freelancerId, CreateInvoiceDto dto)
    {
        var order = await _context.Orders
            .Include(o => o.Invoices)
            .FirstOrDefaultAsync(o => o.Id == orderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.FreelancerId != freelancerId)
            throw new UnauthorizedAccessException("Только назначенный фрилансер может выставить счет");

        if (order.Status != "InProgress")
            throw new InvalidOperationException("Заказ должен быть в процессе выполнения");

        // Проверка: счет уже существует
        if (order.Invoices.Any(i => i.Status == "Pending" || i.Status == "Paid"))
            throw new InvalidOperationException("Счет уже выставлен для этого заказа");

        // Проверка: сумма не превышает бюджет
        if (dto.Amount > order.Budget)
            throw new ArgumentException($"Сумма счета (${dto.Amount}) не может превышать бюджет заказа (${order.Budget})");

        var invoice = new FreeTaskBackend.Models.Invoice
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Amount = dto.Amount,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync();
        return invoice;
    }

    public async Task CompleteOrderAsync(Guid orderId, Guid freelancerId)
    {
        var order = await _context.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.FreelancerId != freelancerId)
            throw new UnauthorizedAccessException("Только назначенный фрилансер может завершить заказ");

        if (order.Status != "InProgress")
            throw new InvalidOperationException("Заказ должен быть в процессе выполнения");

        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        if (payment == null || payment.Status != "Paid")
            throw new InvalidOperationException("Заказ не оплачен");

        order.Status = "CompletedByFreelancer";
        await _context.SaveChangesAsync();
    }
    public async Task CancelOrderAsync(Guid orderId, Guid userId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
            throw new KeyNotFoundException("Order not found");

        if (order.ClientId != userId)
            throw new UnauthorizedAccessException("Only the client can cancel this order");

        if (order.Status != "Open" && order.Status != "InProgress")
            throw new InvalidOperationException("Order cannot be cancelled in its current state");

        order.Status = "Cancelled";

        await _context.SaveChangesAsync();
    }
    public async Task ConfirmOrderAsync(Guid orderId, Guid clientId)
    {
        var order = await _context.Orders.FindAsync(orderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != clientId)
            throw new UnauthorizedAccessException("Только заказчик может подтвердить заказ");

        if (order.Status != "CompletedByFreelancer")
            throw new InvalidOperationException("Заказ должен быть завершен фрилансером");

        order.Status = "Completed";
        await _context.SaveChangesAsync();
    }
}