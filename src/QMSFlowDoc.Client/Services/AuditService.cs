using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IAuditService
{
    Task<List<AuditLogDto>> GetLogsAsync(AuditFilter filter, int maxResults = 200);
    Task LogAsync(string action, string entityType, Guid? entityId, string? details = null);
}

/// <summary>
/// V2: Audit service using SQL Server via EF Core.
/// Note: Most audit logging is done automatically by AuditSaveChangesInterceptor.
/// This service provides query access and manual log creation.
/// </summary>
public class AuditService : IAuditService
{
    private readonly ClientDbContextFactory _dbFactory;

    public AuditService(ClientDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<List<AuditLogDto>> GetLogsAsync(AuditFilter filter, int maxResults = 200)
    {
        using var ctx = _dbFactory.CreateContext();
        var query = ctx.AuditLogs.AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(a => a.Timestamp >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(a => a.Timestamp <= filter.ToDate.Value);
        if (!string.IsNullOrEmpty(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);
        if (!string.IsNullOrEmpty(filter.Action))
            query = query.Where(a => a.Action.Contains(filter.Action));
        if (!string.IsNullOrEmpty(filter.UserName))
            query = query.Where(a => a.UserName != null && a.UserName.Contains(filter.UserName));

        return await query
            .OrderByDescending(a => a.Timestamp)
            .Take(maxResults)
            .Select(a => new AuditLogDto(
                a.Id,
                a.Timestamp,
                a.UserId.ToString(),
                a.UserName,
                a.Action,
                a.EntityType,
                a.EntityId.ToString(),
                a.Details,
                a.MachineName
            ))
            .ToListAsync();
    }

    public async Task LogAsync(string action, string entityType, Guid? entityId, string? details = null)
    {
        using var ctx = _dbFactory.CreateContext();
        var log = new AuditLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            MachineName = Environment.MachineName,
            Result = "OK"
        };
        ctx.AuditLogs.Add(log);
        await ctx.SaveChangesAsync();
    }
}
