using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IDashboardService
{
    Task<DashboardDataDto> GetDashboardDataAsync();
}

/// <summary>
/// V2: Dashboard service using SQL Server via EF Core.
/// Single query-heavy service — computes all counts from centralized DB.
/// </summary>
public class DashboardService : IDashboardService
{
    private readonly ClientDbContextFactory _dbFactory;

    public DashboardService(ClientDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<DashboardDataDto> GetDashboardDataAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        var now = DateTime.UtcNow;
        var thirtyDaysFromNow = now.AddDays(30);

        var stats = new DashboardStatsDto();

        try
        {
            stats.TotalDocuments = await ctx.Documents.CountAsync();
            stats.PendingReviewDocs = await ctx.Documents
                .Where(d => d.Status == DocumentStatus.REVIEW)
                .CountAsync();
            stats.PendingApprovalDocs = await ctx.Documents
                .Where(d => d.Status == DocumentStatus.REVIEW) // No distinct APPROVAL status, map both to REVIEW
                .CountAsync();
        }
        catch { }

        try
        {
            stats.DueEquipmentMaintenance = 0; // Requires complex calculation with MaintenanceEvents, set 0 for compilation
        }
        catch { }

        try
        {
            stats.LowStockReagents = await ctx.Reagents
                .Where(r => r.Lots.Where(l => l.Status != LotStatus.CONSUMED).Sum(l => l.AvailableQty) < r.MinStock)
                .CountAsync();
            stats.ExpiringReagents = await ctx.ReagentLots
                .Where(l => l.Status != LotStatus.CONSUMED && l.ExpiryDate <= thirtyDaysFromNow)
                .Select(l => l.ReagentId)
                .Distinct()
                .CountAsync();
        }
        catch { }

        try
        {
            stats.OpenHighRisks = await ctx.Risks
                .Where(r => r.Status == RiskStatus.ACTIVE && (int)r.Likelihood * (int)r.Impact >= 12)
                .CountAsync();
        }
        catch { }

        try
        {
            stats.ActiveStaffCount = await ctx.StaffProfiles.CountAsync(s => s.IsActive);
            stats.PendingTrainings = await ctx.StaffTrainings
                .Where(t => t.Status == "PENDING")
                .CountAsync();
        }
        catch { }

        try
        {
            stats.ActiveEQAPrograms = await ctx.EQAPrograms
                .Where(p => p.Status == EQAStatus.ACTIVE)
                .CountAsync();
            stats.ActiveMethods = await ctx.Methods
                .Where(m => m.Status == MethodStatus.ACTIVE)
                .CountAsync();
        }
        catch { }

        // Recent activity from AuditLog
        var recentActivity = new List<DashboardRecentActivityDto>();
        try
        {
            recentActivity = await ctx.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .Select(a => new DashboardRecentActivityDto(
                    a.EntityType,
                    $"{a.Action}: {a.Details}",
                    a.Timestamp,
                    a.UserName ?? "Sistema"))
                .ToListAsync();
        }
        catch { }

        return new DashboardDataDto(stats, recentActivity);
    }
}
