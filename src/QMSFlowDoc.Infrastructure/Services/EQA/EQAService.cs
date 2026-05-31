using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.EQA;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Infrastructure.Services.EQA;

public class EQAService : IEQAService
{
    private readonly QmsDbContext _context;

    public EQAService(QmsDbContext context)
    {
        _context = context;
    }

    // ── Programs ─────────────────────────────────────────────────────

    public async Task<List<EQAProgram>> GetProgramsAsync()
    {
        return await _context.EQAPrograms
            .Include(p => p.Enrollments)
            .Include(p => p.TestMappings)
            .OrderBy(p => p.InternalCode)
            .ToListAsync();
    }

    public async Task<EQAProgram?> GetProgramByIdAsync(Guid id)
    {
        return await _context.EQAPrograms
            .Include(p => p.Enrollments)
            .Include(p => p.TestMappings)
            .Include(p => p.Rounds)
                .ThenInclude(r => r.Samples)
            .Include(p => p.Rounds)
                .ThenInclude(r => r.Deviations)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Guid> CreateProgramAsync(EQAProgram program, string? userName = null)
    {
        if (program.Id == Guid.Empty) program.Id = Guid.NewGuid();
        _context.EQAPrograms.Add(program);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "EQAProgram", program.Id, $"Programa EQA '{program.Name}' ({program.InternalCode}) creado", userName);
        await _context.SaveChangesAsync();

        return program.Id;
    }

    public async Task<bool> UpdateProgramAsync(EQAProgram program, string? userName = null)
    {
        var existing = await _context.EQAPrograms.FindAsync(program.Id);
        if (existing == null) return false;

        _context.Entry(existing).CurrentValues.SetValues(program);
        await LogAuditAsync("EDIT", "EQAProgram", program.Id, $"Programa EQA '{program.Name}' modificado", userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteProgramAsync(Guid id, string? userName = null)
    {
        var program = await _context.EQAPrograms.FindAsync(id);
        if (program == null) return false;

        _context.EQAPrograms.Remove(program);
        await LogAuditAsync("DELETE", "EQAProgram", id, $"Programa EQA '{program.Name}' eliminado de forma permanente", userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Enrollments ──────────────────────────────────────────────────

    public async Task<List<EQAEnrollment>> GetEnrollmentsAsync(int? year = null)
    {
        var query = _context.EQAEnrollments.AsQueryable();
        if (year.HasValue)
        {
            query = query.Where(e => e.Year == year.Value);
        }
        return await query.ToListAsync();
    }

    public async Task<Guid> CreateEnrollmentAsync(EQAEnrollment enrollment, string? userName = null)
    {
        if (enrollment.Id == Guid.Empty) enrollment.Id = Guid.NewGuid();
        _context.EQAEnrollments.Add(enrollment);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "EQAEnrollment", enrollment.Id, $"Inscripción EQA para el año {enrollment.Year}", userName);
        await _context.SaveChangesAsync();

        return enrollment.Id;
    }

    public async Task<bool> UpdateEnrollmentAsync(EQAEnrollment enrollment, string? userName = null)
    {
        var existing = await _context.EQAEnrollments.FindAsync(enrollment.Id);
        if (existing == null) return false;

        _context.Entry(existing).CurrentValues.SetValues(enrollment);
        await LogAuditAsync("EDIT", "EQAEnrollment", enrollment.Id, $"Inscripción EQA para el año {enrollment.Year} modificada", userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteEnrollmentAsync(Guid id, string? userName = null)
    {
        var enrollment = await _context.EQAEnrollments.FindAsync(id);
        if (enrollment == null) return false;

        _context.EQAEnrollments.Remove(enrollment);
        await LogAuditAsync("DELETE", "EQAEnrollment", id, $"Inscripción EQA del año {enrollment.Year} eliminada", userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Mappings ─────────────────────────────────────────────────────

    public async Task<List<EQAMapping>> GetMappingsAsync(Guid programId)
    {
        return await _context.EQAMappings
            .Where(m => m.ProgramId == programId)
            .ToListAsync();
    }

    public async Task<Guid> SaveMappingAsync(EQAMapping mapping, string? userName = null)
    {
        if (mapping.Id == Guid.Empty) mapping.Id = Guid.NewGuid();
        var existing = await _context.EQAMappings.FindAsync(mapping.Id);
        
        if (existing == null)
        {
            _context.EQAMappings.Add(mapping);
            await LogAuditAsync("CREATE", "EQAMapping", mapping.Id, $"Mapeo programa-prueba creado para '{mapping.InternalTestName}'", userName);
        }
        else
        {
            _context.Entry(existing).CurrentValues.SetValues(mapping);
            await LogAuditAsync("EDIT", "EQAMapping", mapping.Id, $"Mapeo programa-prueba actualizado para '{mapping.InternalTestName}'", userName);
        }

        await _context.SaveChangesAsync();
        return mapping.Id;
    }

    public async Task<bool> DeleteMappingAsync(Guid id, string? userName = null)
    {
        var mapping = await _context.EQAMappings.FindAsync(id);
        if (mapping == null) return false;

        _context.EQAMappings.Remove(mapping);
        await LogAuditAsync("DELETE", "EQAMapping", id, $"Mapeo de la prueba '{mapping.InternalTestName}' eliminado", userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Rounds ───────────────────────────────────────────────────────

    public async Task<List<EQARound>> GetRoundsAsync(int? year = null)
    {
        var query = _context.EQARounds
            .Include(r => r.Samples)
            .Include(r => r.Deviations)
            .AsQueryable();

        if (year.HasValue)
        {
            query = query.Where(r => r.Year == year.Value);
        }

        return await query.ToListAsync();
    }

    public async Task<EQARound?> GetRoundByIdAsync(Guid id)
    {
        return await _context.EQARounds
            .Include(r => r.Samples)
            .Include(r => r.Deviations)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Guid> CreateRoundAsync(EQARound round, string? userName = null)
    {
        if (round.Id == Guid.Empty) round.Id = Guid.NewGuid();
        
        // Also ensure sample IDs are initialized
        foreach (var sample in round.Samples)
        {
            if (sample.Id == Guid.Empty) sample.Id = Guid.NewGuid();
            sample.RoundId = round.Id;
        }

        _context.EQARounds.Add(round);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "EQARound", round.Id, $"Ronda EQA '{round.ExternalCode}' creada", userName);
        await _context.SaveChangesAsync();

        return round.Id;
    }

    public async Task<bool> UpdateRoundAsync(EQARound round, string? userName = null)
    {
        var existing = await _context.EQARounds
            .Include(r => r.Samples)
            .Include(r => r.Deviations)
            .FirstOrDefaultAsync(r => r.Id == round.Id);

        if (existing == null) return false;

        // Capture audit log for specific changes
        var detailsMsg = $"Ronda EQA '{round.ExternalCode}' modificada (Estado: {round.Status})";
        
        // Update scalar values
        _context.Entry(existing).CurrentValues.SetValues(round);

        // Update Samples collection (adds, edits, removes)
        foreach (var sample in round.Samples)
        {
            var existingSample = existing.Samples.FirstOrDefault(s => s.Id == sample.Id);
            if (existingSample == null)
            {
                if (sample.Id == Guid.Empty) sample.Id = Guid.NewGuid();
                sample.RoundId = round.Id;
                existing.Samples.Add(sample);
            }
            else
            {
                _context.Entry(existingSample).CurrentValues.SetValues(sample);
            }
        }
        
        // Remove samples not in new list
        var sampleIds = round.Samples.Select(s => s.Id).ToList();
        var samplesToRemove = existing.Samples.Where(s => !sampleIds.Contains(s.Id)).ToList();
        foreach (var sample in samplesToRemove)
        {
            existing.Samples.Remove(sample);
        }

        // Update Deviations collection
        foreach (var deviation in round.Deviations)
        {
            var existingDev = existing.Deviations.FirstOrDefault(d => d.Id == deviation.Id);
            if (existingDev == null)
            {
                if (deviation.Id == Guid.Empty) deviation.Id = Guid.NewGuid();
                deviation.RoundId = round.Id;
                existing.Deviations.Add(deviation);
            }
            else
            {
                _context.Entry(existingDev).CurrentValues.SetValues(deviation);
            }
        }

        var devIds = round.Deviations.Select(d => d.Id).ToList();
        var devsToRemove = existing.Deviations.Where(d => !devIds.Contains(d.Id)).ToList();
        foreach (var dev in devsToRemove)
        {
            existing.Deviations.Remove(dev);
        }

        await LogAuditAsync("EDIT", "EQARound", round.Id, detailsMsg, userName);
        return await _context.SaveChangesAsync() > 0;
    }

    public async Task<bool> DeleteRoundAsync(Guid id, string? userName = null)
    {
        var round = await _context.EQARounds.FindAsync(id);
        if (round == null) return false;

        _context.EQARounds.Remove(round);
        await LogAuditAsync("DELETE", "EQARound", id, $"Ronda EQA '{round.ExternalCode}' eliminada", userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Samples ──────────────────────────────────────────────────────

    public async Task<EQASample?> GetSampleByIdAsync(Guid id)
    {
        return await _context.EQASamples.FindAsync(id);
    }

    public async Task<bool> UpdateSampleAsync(EQASample sample, string? userName = null)
    {
        var existing = await _context.EQASamples.FindAsync(sample.Id);
        if (existing == null) return false;

        _context.Entry(existing).CurrentValues.SetValues(sample);
        await LogAuditAsync("EDIT", "EQASample", sample.Id, $"Muestra EQA '{sample.InternalCode}' (Procesamiento/Resultados) actualizada", userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Deviations ───────────────────────────────────────────────────

    public async Task<List<EQADeviation>> GetDeviationsAsync()
    {
        return await _context.EQADeviations
            .OrderByDescending(d => d.Id)
            .ToListAsync();
    }

    public async Task<Guid> CreateDeviationAsync(EQADeviation deviation, string? userName = null)
    {
        if (deviation.Id == Guid.Empty) deviation.Id = Guid.NewGuid();
        _context.EQADeviations.Add(deviation);
        await _context.SaveChangesAsync();

        await LogAuditAsync("CREATE", "EQADeviation", deviation.Id, $"Desviación EQA registrada (Tipo: {deviation.DeviationType})", userName);
        await _context.SaveChangesAsync();

        return deviation.Id;
    }

    public async Task<bool> UpdateDeviationAsync(EQADeviation deviation, string? userName = null)
    {
        var existing = await _context.EQADeviations.FindAsync(deviation.Id);
        if (existing == null) return false;

        _context.Entry(existing).CurrentValues.SetValues(deviation);
        await LogAuditAsync("EDIT", "EQADeviation", deviation.Id, $"Desviación EQA '{deviation.Id}' actualizada (Estado: {deviation.Status})", userName);
        return await _context.SaveChangesAsync() > 0;
    }

    // ── Audit Logging Helper ──────────────────────────────────────────

    private async Task LogAuditAsync(string action, string entityType, Guid? entityId, string details, string? username)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            UserId = Guid.Empty, // System or default action
            UserName = username ?? "Sistema",
            Timestamp = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            Result = "Success"
        };
        _context.AuditLogs.Add(audit);
    }
}
