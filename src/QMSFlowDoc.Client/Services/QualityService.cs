using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IQualityService
{
    Task<IEnumerable<NCListDto>> GetNonconformitiesAsync();
    Task<Nonconformity?> GetNCByIdAsync(Guid id);
    Task<Nonconformity?> CreateNCAsync(CreateNCRequest request);
    Task<bool> UpdateNCAsync(Guid id, CreateNCRequest request);
    Task<bool> UpdateNCStatusAsync(Guid id, int status);
    Task<CapaAction?> CreateCAPAAsync(CreateCAPARequest request);
}

/// <summary>
/// V2: Quality service using SQL Server via EF Core.
/// </summary>
public class QualityService : IQualityService
{
    private readonly ClientDbContextFactory _dbFactory;

    public QualityService(ClientDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IEnumerable<NCListDto>> GetNonconformitiesAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Nonconformities
            .Include(nc => nc.Actions)
            .OrderByDescending(nc => nc.DetectedAt)
            .Select(nc => new NCListDto
            {
                Id = nc.Id,
                Title = nc.Title,
                Severity = nc.Severity,
                Status = nc.Status,
                Origin = nc.Origin,
                DetectedAt = nc.DetectedAt,
                ActionCount = nc.Actions.Count,
                ImpactPatient = nc.ImpactPatient
            })
            .ToListAsync();
    }

    public async Task<Nonconformity?> GetNCByIdAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Nonconformities
            .Include(nc => nc.Actions)
            .FirstOrDefaultAsync(nc => nc.Id == id);
    }

    public async Task<Nonconformity?> CreateNCAsync(CreateNCRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var nc = new Nonconformity
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            Severity = request.Severity,
            Status = request.Status ?? NCStatus.OPEN,
            ImpactPatient = request.ImpactPatient,
            Containment = request.Containment,
            Origin = request.Origin,
            RootCauseAnalysis = request.RootCauseAnalysis,
            DetectedAt = DateTime.UtcNow,
            DetectedByUserId = request.DetectedByUserId,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Nonconformities.Add(nc);
        await ctx.SaveChangesAsync();
        return nc;
    }

    public async Task<bool> UpdateNCAsync(Guid id, CreateNCRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var nc = await ctx.Nonconformities.FindAsync(id);
        if (nc == null) return false;

        nc.Title = request.Title;
        nc.Description = request.Description;
        nc.Severity = request.Severity;
        if (request.Status.HasValue) nc.Status = request.Status.Value;
        nc.ImpactPatient = request.ImpactPatient;
        nc.Containment = request.Containment;
        nc.Origin = request.Origin;
        nc.RootCauseAnalysis = request.RootCauseAnalysis;
        nc.UpdatedAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateNCStatusAsync(Guid id, int status)
    {
        using var ctx = _dbFactory.CreateContext();
        var nc = await ctx.Nonconformities.FindAsync(id);
        if (nc == null) return false;

        nc.Status = (NCStatus)status;
        nc.UpdatedAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<CapaAction?> CreateCAPAAsync(CreateCAPARequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var action = new CapaAction
        {
            Id = Guid.NewGuid(),
            NCId = request.NCId,
            ActionType = request.ActionType,
            Description = request.Description,
            OwnerUserId = request.OwnerUserId,
            DueDate = request.DueDate,
            Status = CAPAStatus.OPEN
        };
        ctx.CapaActions.Add(action);
        await ctx.SaveChangesAsync();
        return action;
    }
}
