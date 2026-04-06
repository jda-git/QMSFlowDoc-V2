using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface ISupplierService
{
    Task<List<SupplierListDto>> GetSuppliersAsync();
    Task<SupplierDetailDto?> GetSupplierByIdAsync(Guid id);
    Task<SupplierDetailDto?> CreateSupplierAsync(CreateSupplierRequest request);
    Task<bool> UpdateSupplierAsync(SupplierDetailDto dto);
    Task<bool> DeleteSupplierAsync(Guid id);
    Task<List<SupplierEvaluation>> GetEvaluationsAsync(Guid supplierId);
    Task<SupplierEvaluation?> CreateEvaluationAsync(CreateSupplierEvaluationRequest request, Guid? evaluatorUserId);
}

/// <summary>V2: Supplier service using SQL Server via EF Core.</summary>
public class SupplierService : ISupplierService
{
    private readonly ClientDbContextFactory _dbFactory;

    public SupplierService(ClientDbContextFactory dbFactory) => _dbFactory = dbFactory;

    public async Task<List<SupplierListDto>> GetSuppliersAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.Suppliers
            .Include(s => s.Evaluations)
            .OrderBy(s => s.Name)
            .Select(s => new SupplierListDto(s.Id, s.Name, s.Type, s.QualityStatus,
                s.Evaluations.OrderByDescending(e => e.EvaluationDate).Select(e => (DateTime?)e.EvaluationDate).FirstOrDefault(),
                null,
                s.Evaluations.Count, 0))
            .ToListAsync();
    }

    public async Task<SupplierDetailDto?> GetSupplierByIdAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        var s = await ctx.Suppliers.Include(x => x.Evaluations).FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return null;

        return new SupplierDetailDto
        {
            Id = s.Id,
            Name = s.Name,
            ContactName = s.ContactName,
            Email = s.Email,
            Phone = s.Phone,
            Address = s.Address,
            Notes = s.Notes,
            Type = s.Type,
            QualityStatus = s.QualityStatus,
            LastEvaluationDate = s.Evaluations.OrderByDescending(e => e.EvaluationDate).Select(e => (DateTime?)e.EvaluationDate).FirstOrDefault(),
            CreatedAt = s.CreatedAt,
            Evaluations = s.Evaluations.Select(e => new SupplierEvaluationDto
            {
                 Id = e.Id,
                 SupplierId = e.SupplierId,
                 EvaluationDate = e.EvaluationDate,
                 EvaluatedPeriod = e.EvaluatedPeriod,
                 ScorePlazos = e.ScorePlazos,
                 ScoreCalidad = e.ScoreCalidad,
                 ScoreServicio = e.ScoreServicio,
                 ScoreIncidencias = e.ScoreIncidencias,
                 IsApproved = e.IsApproved,
                 Observations = e.Observations,
                 AttachmentPath = e.AttachmentPath
            }).ToList()
        };
    }

    public async Task<SupplierDetailDto?> CreateSupplierAsync(CreateSupplierRequest request)
    {
        using var ctx = _dbFactory.CreateContext();
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            ContactName = request.ContactName,
            Email = request.Email,
            Phone = request.Phone,
            Address = request.Address,
            Notes = request.Notes,
            Type = request.Type,
            QualityStatus = SupplierQualityStatus.PENDIENTE,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        ctx.Suppliers.Add(supplier);
        await ctx.SaveChangesAsync();
        return await GetSupplierByIdAsync(supplier.Id);
    }

    public async Task<bool> UpdateSupplierAsync(SupplierDetailDto dto)
    {
        using var ctx = _dbFactory.CreateContext();
        var existing = await ctx.Suppliers.FindAsync(dto.Id);
        if (existing == null) return false;

        existing.Name = dto.Name;
        existing.ContactName = dto.ContactName;
        existing.Email = dto.Email;
        existing.Phone = dto.Phone;
        existing.Address = dto.Address;
        existing.Notes = dto.Notes;
        existing.Type = dto.Type;
        existing.UpdatedAt = DateTime.UtcNow;

        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteSupplierAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        var s = await ctx.Suppliers.FindAsync(id);
        if (s == null) return false;
        s.IsDeleted = true;
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<List<SupplierEvaluation>> GetEvaluationsAsync(Guid supplierId)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.SupplierEvaluations
            .Where(e => e.SupplierId == supplierId)
            .OrderByDescending(e => e.EvaluationDate)
            .ToListAsync();
    }

    public async Task<SupplierEvaluation?> CreateEvaluationAsync(CreateSupplierEvaluationRequest request, Guid? evaluatorUserId)
    {
        using var ctx = _dbFactory.CreateContext();
        var evaluation = new SupplierEvaluation
        {
            Id = Guid.NewGuid(),
            SupplierId = request.SupplierId,
            EvaluationDate = request.EvaluationDate,
            EvaluatedPeriod = request.EvaluatedPeriod,
            ScorePlazos = request.ScorePlazos,
            ScoreCalidad = request.ScoreCalidad,
            ScoreServicio = request.ScoreServicio,
            ScoreIncidencias = request.ScoreIncidencias,
            IsApproved = request.IsApproved,
            Observations = request.Observations,
            AttachmentPath = request.AttachmentPath,
            EvaluatorUserId = evaluatorUserId,
            CreatedAt = DateTime.UtcNow
        };
        ctx.SupplierEvaluations.Add(evaluation);
        await ctx.SaveChangesAsync();
        return evaluation;
    }
}
