using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;

namespace FreeTaskBackend.Services;

public class ModerationService
{
    private readonly AppDbContext _context;

    public ModerationService(AppDbContext context)
    {
        _context = context;
    }

    public async Task ReportContentAsync(Guid userId, ReportContentDto dto)
    {
        var report = new Report
        {
            Id = Guid.NewGuid(),
            ReporterId = userId,
            TargetId = dto.TargetId,
            TargetType = dto.TargetType,
            Reason = dto.Reason,
            CreatedAt = DateTime.UtcNow
        };
        _context.Reports.Add(report);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Report>> GetPendingReportsAsync(int page, int pageSize)
    {
        return await _context.Reports
            .Where(r => !r.IsResolved)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }
    public async Task<bool> TargetExistsAsync(string targetType, Guid targetId)
    {
        return targetType switch
        {
            "Order" => await _context.Orders.AnyAsync(o => o.Id == targetId),
            "Message" => await _context.Messages.AnyAsync(m => m.Id == targetId),
            _ => false
        };
    }
}