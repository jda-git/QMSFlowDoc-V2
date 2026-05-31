using QMSFlowDoc.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QMSFlowDoc.Application.Services.EQA;

public interface IEQAService
{
    // Programs
    Task<List<EQAProgram>> GetProgramsAsync();
    Task<EQAProgram?> GetProgramByIdAsync(Guid id);
    Task<Guid> CreateProgramAsync(EQAProgram program, string? userName = null);
    Task<bool> UpdateProgramAsync(EQAProgram program, string? userName = null);
    Task<bool> DeleteProgramAsync(Guid id, string? userName = null);

    // Enrollments
    Task<List<EQAEnrollment>> GetEnrollmentsAsync(int? year = null);
    Task<Guid> CreateEnrollmentAsync(EQAEnrollment enrollment, string? userName = null);
    Task<bool> UpdateEnrollmentAsync(EQAEnrollment enrollment, string? userName = null);
    Task<bool> DeleteEnrollmentAsync(Guid id, string? userName = null);

    // Mappings
    Task<List<EQAMapping>> GetMappingsAsync(Guid programId);
    Task<Guid> SaveMappingAsync(EQAMapping mapping, string? userName = null);
    Task<bool> DeleteMappingAsync(Guid id, string? userName = null);

    // Rounds
    Task<List<EQARound>> GetRoundsAsync(int? year = null);
    Task<EQARound?> GetRoundByIdAsync(Guid id);
    Task<Guid> CreateRoundAsync(EQARound round, string? userName = null);
    Task<bool> UpdateRoundAsync(EQARound round, string? userName = null);
    Task<bool> DeleteRoundAsync(Guid id, string? userName = null);

    // Samples
    Task<EQASample?> GetSampleByIdAsync(Guid id);
    Task<bool> UpdateSampleAsync(EQASample sample, string? userName = null);

    // Deviations & CAPA
    Task<List<EQADeviation>> GetDeviationsAsync();
    Task<Guid> CreateDeviationAsync(EQADeviation deviation, string? userName = null);
    Task<bool> UpdateDeviationAsync(EQADeviation deviation, string? userName = null);
}
