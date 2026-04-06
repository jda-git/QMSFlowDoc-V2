using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IMethodService
{
    Task<List<MethodDto>> GetMethodsAsync();
    Task<MethodDto?> GetMethodByIdAsync(Guid id);
    Task<MethodDto?> CreateMethodAsync(CreateMethodRequest req);
    Task<bool> UpdateMethodAsync(UpdateMethodRequest req);

    Task<List<MethodAuthorizationDto>> GetAuthorizationsAsync(Guid methodId);
    Task<bool> AuthorizeUserAsync(AuthorizeMethodRequest req);
    Task<bool> RemoveAuthorizationAsync(Guid authId);

    Task<List<MethodVersionDto>> GetVersionsAsync(Guid methodId);
    Task<bool> CreateVersionAsync(CreateMethodVersionRequest req);
    Task<bool> ApproveVersionAsync(Guid versionId, string approverName);

    Task<List<MethodValidationDto>> GetValidationsAsync(Guid versionId);
    Task<bool> AddValidationAsync(MethodValidationDto validation);
}

/// <summary>V2: Method service using SQL Server via EF Core.</summary>
public class MethodService : IMethodService
{
    private readonly ClientDbContextFactory _dbFactory;

    public MethodService(ClientDbContextFactory dbFactory) => _dbFactory = dbFactory;

    public async Task<List<MethodDto>> GetMethodsAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        var methods = await ctx.Methods
            .Include(m => m.Authorizations)
            .OrderBy(m => m.Code)
            .ToListAsync();

        return methods.Select(m => new MethodDto
        {
            Id = m.Id,
            Code = m.Code,
            Name = m.Name,
            Category = m.Category,
            Status = m.Status,
            StatusDisplay = m.Status.ToString(),
            CurrentVersion = m.CurrentVersion,
            EffectiveDate = m.EffectiveDate,
            DocumentId = m.DocumentId,
            Notes = m.Notes,
            AuthorizedUsersCount = m.Authorizations.Count
        }).ToList();
    }

    public async Task<MethodDto?> GetMethodByIdAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        var m = await ctx.Methods
            .Include(x => x.Authorizations)
            .FirstOrDefaultAsync(x => x.Id == id);
            
        if (m == null) return null;

        return new MethodDto
        {
            Id = m.Id,
            Code = m.Code,
            Name = m.Name,
            Category = m.Category,
            Status = m.Status,
            StatusDisplay = m.Status.ToString(),
            CurrentVersion = m.CurrentVersion,
            EffectiveDate = m.EffectiveDate,
            DocumentId = m.DocumentId,
            Notes = m.Notes,
            AuthorizedUsersCount = m.Authorizations.Count
        };
    }

    public async Task<MethodDto?> CreateMethodAsync(CreateMethodRequest req)
    {
        using var ctx = _dbFactory.CreateContext();
        var method = new Method
        {
            Id = Guid.NewGuid(),
            Code = req.Code,
            Name = req.Name,
            Category = req.Category,
            CurrentVersion = req.CurrentVersion,
            DocumentId = req.DocumentId,
            Notes = req.Notes,
            Status = MethodStatus.DRAFT,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Methods.Add(method);
        await ctx.SaveChangesAsync();
        return await GetMethodByIdAsync(method.Id);
    }

    public async Task<bool> UpdateMethodAsync(UpdateMethodRequest req)
    {
        using var ctx = _dbFactory.CreateContext();
        var existing = await ctx.Methods.FindAsync(req.Id);
        if (existing == null) return false;

        existing.Code = req.Code;
        existing.Name = req.Name;
        existing.Category = req.Category;
        existing.Status = req.Status;
        existing.CurrentVersion = req.CurrentVersion;
        existing.EffectiveDate = req.EffectiveDate;
        existing.DocumentId = req.DocumentId;
        existing.Notes = req.Notes;
        existing.UpdatedAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<List<MethodAuthorizationDto>> GetAuthorizationsAsync(Guid methodId)
    {
        using var ctx = _dbFactory.CreateContext();
        
        var auths = await ctx.MethodAuthorizations
            .Where(a => a.MethodId == methodId)
            .ToListAsync();
            
        var users = await ctx.Users.ToListAsync();

        return auths.Select(a => new MethodAuthorizationDto
        {
            Id = a.Id,
            UserId = a.UserId,
            UserName = users.FirstOrDefault(u => u.Id == a.UserId)?.FullName ?? "Desconocido",
            AuthorizedAt = a.AuthorizedAt,
            ExpiresAt = a.ExpiresAt,
            AuthorizedByName = users.FirstOrDefault(u => u.Id == a.AuthorizedByUserId)?.FullName
        }).ToList();
    }

    public async Task<bool> AuthorizeUserAsync(AuthorizeMethodRequest req)
    {
        using var ctx = _dbFactory.CreateContext();
        var auth = new MethodAuthorization
        {
            Id = Guid.NewGuid(),
            MethodId = req.MethodId,
            UserId = req.UserId,
            AuthorizedAt = DateTime.UtcNow,
            ExpiresAt = req.ExpiresAt,
            AuthorizedByUserId = req.AuthorizedByUserId
        };
        ctx.MethodAuthorizations.Add(auth);
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAuthorizationAsync(Guid authId)
    {
        using var ctx = _dbFactory.CreateContext();
        var auth = await ctx.MethodAuthorizations.FindAsync(authId);
        if (auth != null)
        {
            ctx.MethodAuthorizations.Remove(auth);
            await ctx.SaveChangesAsync();
            return true;
        }
        return false;
    }

    public async Task<List<MethodVersionDto>> GetVersionsAsync(Guid methodId)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.MethodVersions
            .Where(v => v.MethodId == methodId)
            .OrderByDescending(v => v.CreatedAt)
            .Select(v => new MethodVersionDto(v.Id, v.MethodId, v.Version, v.Status, v.ChangeDescription, v.DocumentPath, v.CreatedBy, v.CreatedAt, v.ApprovedBy, v.ApprovedAt))
            .ToListAsync();
    }

    public async Task<bool> CreateVersionAsync(CreateMethodVersionRequest req)
    {
        using var ctx = _dbFactory.CreateContext();
        var ver = new MethodVersion
        {
            Id = Guid.NewGuid(),
            MethodId = req.MethodId,
            Version = req.Version,
            Status = "DRAFT",
            ChangeDescription = req.ChangeDescription,
            DocumentPath = req.DocumentPath,
            CreatedBy = req.CreatedBy,
            CreatedAt = DateTime.UtcNow
        };
        ctx.MethodVersions.Add(ver);
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApproveVersionAsync(Guid versionId, string approverName)
    {
        using var ctx = _dbFactory.CreateContext();
        var ver = await ctx.MethodVersions.FindAsync(versionId);
        if (ver == null) return false;

        ver.Status = "APPROVED";
        ver.ApprovedBy = approverName;
        ver.ApprovedAt = DateTime.UtcNow;

        var method = await ctx.Methods.FindAsync(ver.MethodId);
        if (method != null)
        {
            method.CurrentVersion = ver.Version;
            method.EffectiveDate = DateTime.UtcNow;
            method.DocumentId = !string.IsNullOrEmpty(ver.DocumentPath) && Guid.TryParse(ver.DocumentPath, out var g) ? g : null;
        }

        var obsoleteVersions = await ctx.MethodVersions
            .Where(v => v.MethodId == ver.MethodId && v.Id != ver.Id && v.Status == "APPROVED")
            .ToListAsync();
        foreach (var old in obsoleteVersions)
        {
            old.Status = "OBSOLETE";
        }

        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<List<MethodValidationDto>> GetValidationsAsync(Guid versionId)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.MethodValidations
            .Where(v => v.MethodVersionId == versionId)
            .Select(v => new MethodValidationDto(v.Id, v.MethodVersionId, v.Parameter, v.Result, v.ExperimentCount, v.ReportPath, v.Notes))
            .ToListAsync();
    }

    public async Task<bool> AddValidationAsync(MethodValidationDto validation)
    {
        using var ctx = _dbFactory.CreateContext();
        var val = new MethodValidation
        {
            Id = Guid.NewGuid(),
            MethodVersionId = validation.MethodVersionId,
            Parameter = validation.Parameter,
            Result = validation.Result,
            ExperimentCount = validation.ExperimentCount,
            ReportPath = validation.ReportPath,
            Notes = validation.Notes
        };
        ctx.MethodValidations.Add(val);
        await ctx.SaveChangesAsync();
        return true;
    }
}
