using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface IEQAService
{
    Task<List<EQAProgram>> GetProgramsAsync();
    Task<EQAProgram?> GetProgramByIdAsync(Guid id);
    Task<EQAProgram?> CreateProgramAsync(EQAProgram program);
    Task<bool> UpdateProgramAsync(EQAProgram program);
    Task<EQAResult?> AddResultAsync(EQAResult result);
    Task<bool> UpdateResultAsync(EQAResult result);

    // UI compatibility methods for EQARoundDetailsView
    Task<List<EQASchemeDto>> GetSchemesAsync();
    Task<List<EQARoundDto>> GetRoundsAsync(Guid schemeId);
    Task<EQARoundDto?> GetRoundAsync(Guid roundId);
    Task UpsertRoundAsync(EQARoundDto round);
    Task ApproveRoundAsync(Guid roundId, Guid userId, string userName);
    Task<List<EQARoundResultDto>> GetRoundResultsAsync(Guid roundId);
    Task UpsertRoundResultAsync(EQARoundResultDto result);
}

/// <summary>V2: EQA service using SQL Server via EF Core.</summary>
public class EQAService : IEQAService
{
    private readonly ClientDbContextFactory _dbFactory;

    public EQAService(ClientDbContextFactory dbFactory) => _dbFactory = dbFactory;

    public async Task<List<EQAProgram>> GetProgramsAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.EQAPrograms
            .Include(p => p.Results.OrderByDescending(r => r.ReceiptDate))
            .OrderBy(p => p.Name)
            .ToListAsync();
    }

    public async Task<EQAProgram?> GetProgramByIdAsync(Guid id)
    {
        using var ctx = _dbFactory.CreateContext();
        return await ctx.EQAPrograms
            .Include(p => p.Results)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<EQAProgram?> CreateProgramAsync(EQAProgram program)
    {
        using var ctx = _dbFactory.CreateContext();
        program.Id = Guid.NewGuid();
        ctx.EQAPrograms.Add(program);
        await ctx.SaveChangesAsync();
        return program;
    }

    public async Task<bool> UpdateProgramAsync(EQAProgram program)
    {
        using var ctx = _dbFactory.CreateContext();
        var existing = await ctx.EQAPrograms.FindAsync(program.Id);
        if (existing == null) return false;

        ctx.Entry(existing).CurrentValues.SetValues(program);
        await ctx.SaveChangesAsync();
        return true;
    }

    public async Task<EQAResult?> AddResultAsync(EQAResult result)
    {
        using var ctx = _dbFactory.CreateContext();
        result.Id = Guid.NewGuid();
        ctx.EQAResults.Add(result);
        await ctx.SaveChangesAsync();
        return result;
    }

    public async Task<bool> UpdateResultAsync(EQAResult result)
    {
        using var ctx = _dbFactory.CreateContext();
        var existing = await ctx.EQAResults.FindAsync(result.Id);
        if (existing == null) return false;

        ctx.Entry(existing).CurrentValues.SetValues(result);
        await ctx.SaveChangesAsync();
        return true;
    }

    // --- UI Compatibility Methods ---
    
    public async Task<List<EQASchemeDto>> GetSchemesAsync()
    {
        using var ctx = _dbFactory.CreateContext();
        var programs = await ctx.EQAPrograms.Include(p => p.Results).OrderBy(p => p.Name).ToListAsync();
        return programs.Select(p => new EQASchemeDto(
            p.Id,
            p.Name,
            p.Provider ?? "Unknown",
            string.Empty, // ProviderReference
            p.CycleFrequency, // Periodicity
            p.Status.ToString(),
            string.Empty, // Notes
            0, // TotalRounds expected
            p.Results.Count,
            p.Results.Count(r => r.Performance == EQAPerformance.UNSATISFACTORY),
            string.Empty, // Matrix
            null // ResponsibleUserId
        )).ToList();
    }

    public async Task<List<EQARoundDto>> GetRoundsAsync(Guid schemeId)
    {
        using var ctx = _dbFactory.CreateContext();
        var program = await ctx.EQAPrograms.FindAsync(schemeId);
        if (program == null) return new List<EQARoundDto>();

        var results = await ctx.EQAResults.Where(r => r.ProgramId == schemeId).OrderByDescending(r => r.ReceiptDate).ToListAsync();
        return results.Select(r => new EQARoundDto(
            r.Id,
            r.ProgramId,
            program.Name,
            r.CycleIdentifier,
            r.ReceiptDate?.Year ?? DateTime.Now.Year,
            r.ReceiptDate,
            r.ProcessingDate,
            r.SubmissionDate,
            r.SubmissionDate,
            r.ReviewDate,
            r.ReviewDate,
            r.Status.ToString(),
            "", // FolderPath
            r.Notes,
            r.ReviewerUserId,
            "Revisor",
            r.ReviewDate
        )).ToList();
    }

    public async Task<EQARoundDto?> GetRoundAsync(Guid roundId)
    {
        using var ctx = _dbFactory.CreateContext();
        var result = await ctx.EQAResults.FindAsync(roundId);
        if (result == null) return null;

        var program = await ctx.EQAPrograms.FindAsync(result.ProgramId);

        return new EQARoundDto(
            result.Id,
            result.ProgramId,
            program?.Name ?? "Unknown",
            result.CycleIdentifier,
            result.ReceiptDate?.Year ?? DateTime.Now.Year,
            result.ReceiptDate,
            result.ProcessingDate,
            result.SubmissionDate,
            result.SubmissionDate,
            result.ReviewDate,
            result.ReviewDate,
            result.Status.ToString(),
            "", // FolderPath
            result.Notes,
            result.ReviewerUserId,
            "Revisor",
            result.ReviewDate
        );
    }

    public async Task UpsertRoundAsync(EQARoundDto round)
    {
        using var ctx = _dbFactory.CreateContext();
        var result = await ctx.EQAResults.FindAsync(round.Id);
        if (result != null)
        {
            result.CycleIdentifier = round.RoundCode;
            result.ReceiptDate = round.DateReceived;
            result.ProcessingDate = round.DateAnalysis;
            result.SubmissionDate = round.DateSubmitted;
            result.Notes = round.Notes;
            await ctx.SaveChangesAsync();
        }
    }

    public async Task ApproveRoundAsync(Guid roundId, Guid userId, string userName)
    {
        using var ctx = _dbFactory.CreateContext();
        var result = await ctx.EQAResults.FindAsync(roundId);
        if (result != null)
        {
            result.Status = EQAResultStatus.EVALUATED;
            result.ReviewerUserId = userId;
            result.ReviewDate = DateTime.Now;
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<List<EQARoundResultDto>> GetRoundResultsAsync(Guid roundId)
    {
        using var ctx = _dbFactory.CreateContext();
        var result = await ctx.EQAResults.FindAsync(roundId);
        if (result == null || string.IsNullOrWhiteSpace(result.Notes) || !result.Notes.StartsWith("["))
            return new List<EQARoundResultDto>();

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<EQARoundResultDto>>(result.Notes) ?? new List<EQARoundResultDto>();
        }
        catch
        {
            return new List<EQARoundResultDto>();
        }
    }

    public async Task UpsertRoundResultAsync(EQARoundResultDto resultDto)
    {
        using var ctx = _dbFactory.CreateContext();
        var result = await ctx.EQAResults.FindAsync(resultDto.RoundId);
        if (result != null)
        {
            var lst = await GetRoundResultsAsync(resultDto.RoundId);
            var existing = lst.FirstOrDefault(x => x.Id == resultDto.Id);
            if (existing != null) lst.Remove(existing);
            lst.Add(resultDto);
            
            result.Notes = System.Text.Json.JsonSerializer.Serialize(lst);
            await ctx.SaveChangesAsync();
        }
    }
}
