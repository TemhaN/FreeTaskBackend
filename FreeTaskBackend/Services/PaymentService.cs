using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class PaymentService
{
    private readonly AppDbContext _context;

    public PaymentService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Payment> CreateTestPaymentAsync(Guid userId, CreateTestPaymentDto dto)
    {
        var order = await _context.Orders.FindAsync(dto.OrderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId)
            throw new UnauthorizedAccessException("Только заказчик может создать платеж");

        var existingPayments = await _context.Payments
            .CountAsync(p => p.OrderId == dto.OrderId && p.IsTest && p.CreatedAt.Month == DateTime.UtcNow.Month);
        if (existingPayments >= 2)
            throw new InvalidOperationException("Лимит тестовых платежей (2 в месяц) превышен");

        var paymentIntentService = new PaymentIntentService();
        var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = (long)(dto.Amount * 100),
            Currency = "usd",
            CaptureMethod = "manual",
            PaymentMethodTypes = new List<string> { "card" }
        });

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = dto.OrderId,
            Amount = dto.Amount,
            Status = "Pending",
            StripePaymentId = paymentIntent.Id,
            IsTest = true,
            CreatedAt = DateTime.UtcNow
        };
        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return payment;
    }

    public async Task ReleasePaymentAsync(Guid paymentId, Guid userId)
    {
        var payment = await _context.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException("Платеж не найден");

        var order = await _context.Orders.FindAsync(payment.OrderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId)
            throw new UnauthorizedAccessException("Только заказчик может выпустить средства");

        var paymentIntentService = new PaymentIntentService();
        await paymentIntentService.CaptureAsync(payment.StripePaymentId);

        payment.Status = "Released";
        await _context.SaveChangesAsync();
    }

    public async Task RefundPaymentAsync(Guid paymentId, Guid userId)
    {
        var payment = await _context.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException("Платеж не найден");

        var order = await _context.Orders.FindAsync(payment.OrderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId)
            throw new UnauthorizedAccessException("Только заказчик может вернуть средства");

        var refundService = new RefundService();
        await refundService.CreateAsync(new RefundCreateOptions
        {
            PaymentIntent = payment.StripePaymentId
        });

        payment.Status = "Refunded";
        await _context.SaveChangesAsync();
    }

    public async Task<Payment> GetPaymentStatusAsync(Guid paymentId, Guid userId)
    {
        var payment = await _context.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException(" Платеж не найден");

        var order = await _context.Orders.FindAsync(payment.OrderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId && order.FreelancerId != userId)
            throw new UnauthorizedAccessException("Доступ запрещен");

        return payment;
    }

    public async Task OpenDisputeAsync(Guid paymentId, Guid userId, OpenDisputeDto dto)
    {
        var payment = await _context.Payments.FindAsync(paymentId)
            ?? throw new KeyNotFoundException("Платеж не найден");

        var order = await _context.Orders.FindAsync(payment.OrderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId && order.FreelancerId != userId)
            throw new UnauthorizedAccessException("Только участники заказа могут открыть спор");

        payment.Status = "Disputed";
        await _context.SaveChangesAsync();
    }
    public async Task<Payment> CreatePaymentAsync(Guid userId, CreatePaymentDto dto)
    {
        var order = await _context.Orders.FindAsync(dto.OrderId)
            ?? throw new KeyNotFoundException("Заказ не найден");

        if (order.ClientId != userId)
            throw new UnauthorizedAccessException("Только заказчик может создать платеж");

        var invoice = await _context.Invoices.FindAsync(dto.InvoiceId)
            ?? throw new InvalidOperationException("Счет не найден");

        if (invoice.OrderId != dto.OrderId || invoice.Status != "Pending")
            throw new InvalidOperationException("Счет недействителен или не относится к этому заказу");

        // Предполагаем, что invoice.Amount в тенге, а dto.Amount в тиынах
        if (invoice.Amount * 100 != dto.Amount)
            throw new InvalidOperationException("Сумма платежа не соответствует счету");

        var paymentIntentService = new PaymentIntentService();
        var paymentIntent = await paymentIntentService.CreateAsync(new PaymentIntentCreateOptions
        {
            Amount = (long)(dto.Amount),
            Currency = "kzt",
            CaptureMethod = "automatic",
            PaymentMethodTypes = new List<string> { "card" }
        });

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            OrderId = dto.OrderId,
            InvoiceId = invoice.Id,
            Amount = (long)(dto.Amount),
            Status = "Paid",
            StripePaymentId = paymentIntent.Id,
            IsTest = false,
            CreatedAt = DateTime.UtcNow
        };
        _context.Payments.Add(payment);

        invoice.Status = "Paid";
        await _context.SaveChangesAsync();

        return payment;
    }
}