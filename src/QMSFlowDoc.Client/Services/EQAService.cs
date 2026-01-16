using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Client.Services;

public interface IEQAService
{
    Task<List<EQAProgramDto>> GetProgramsAsync();
    Task<EQAProgram> CreateProgramAsync(CreateEQAProgramRequest request);
    Task UpdateProgramAsync(UpdateEQAProgramRequest request);
    Task<List<EQAResultDto>> GetResultsAsync(Guid programId);
    Task RegisterResultAsync(RegisterEQAResultRequest request);
    Task UpdateResultAsync(UpdateEQAResultRequest request);

    // Phase 3 Methods
    Task<List<EQASchemeDto>> GetSchemesAsync(Guid? providerId = null);
    Task<EQASchemeDto?> GetSchemeAsync(Guid id);
    Task<List<EQARoundDto>> GetRoundsAsync(Guid schemeId);
    Task<EQARoundDto?> GetRoundAsync(Guid id);
    Task UpsertRoundAsync(EQARoundDto round);
    Task<List<EQARoundResultDto>> GetRoundResultsAsync(Guid roundId);
    Task UpsertRoundResultAsync(EQARoundResultDto result);
    Task ApproveRoundAsync(Guid roundId, Guid userId, string userName);
}

public class EQAService : IEQAService
{
    private readonly LocalDocumentStore _localStore;

    public EQAService(LocalDocumentStore localStore)
    {
        _localStore = localStore;
    }

    public async Task<List<EQAProgramDto>> GetProgramsAsync()
    {
        return await _localStore.GetEQAProgramsAsync();
    }

    public async Task<EQAProgram> CreateProgramAsync(CreateEQAProgramRequest request)
    {
        return await _localStore.CreateEQAProgramAsync(request);
    }

    public async Task UpdateProgramAsync(UpdateEQAProgramRequest request)
    {
        await _localStore.UpdateEQAProgramAsync(request);
    }

    public async Task<List<EQAResultDto>> GetResultsAsync(Guid programId)
    {
        return await _localStore.GetEQAResultsAsync(programId);
    }

    public async Task RegisterResultAsync(RegisterEQAResultRequest request)
    {
        await _localStore.RegisterEQAResultAsync(request);
    }

    public async Task UpdateResultAsync(UpdateEQAResultRequest request)
    {
        await _localStore.UpdateEQAResultAsync(request);
    }

    // Phase 3 Implementation
    public async Task<List<EQASchemeDto>> GetSchemesAsync(Guid? providerId = null)
    {
        return await _localStore.GetEQASchemesAsync(providerId);
    }

    public async Task<EQASchemeDto?> GetSchemeAsync(Guid id)
    {
        return await _localStore.GetEQASchemeByIdAsync(id);
    }

    public async Task<List<EQARoundDto>> GetRoundsAsync(Guid schemeId)
    {
        return await _localStore.GetEQARoundsAsync(schemeId);
    }

    public async Task<EQARoundDto?> GetRoundAsync(Guid id)
    {
        return await _localStore.GetEQARoundByIdAsync(id);
    }

    public async Task UpsertRoundAsync(EQARoundDto round)
    {
        // Handle folder creation if path is null
        if (string.IsNullOrEmpty(round.FolderPath))
        {
            var scheme = await _localStore.GetEQASchemeByIdAsync(round.SchemeId);
            if (scheme != null)
            {
                try
                {
                    var baseDir = await _localStore.GetBaseDocumentPathAsync();
                    if (!string.IsNullOrEmpty(baseDir))
                    {
                        // Structure: QMS\EQA\Provider\Scheme\RoundCode
                        var roundDir = System.IO.Path.Combine(baseDir, "EQA", 
                            scheme.ProviderName.Replace(" ", "_"), 
                            scheme.Name.Replace(" ", "_"), 
                            round.RoundCode.Replace(" ", "_"));

                        if (!System.IO.Directory.Exists(roundDir))
                        {
                            System.IO.Directory.CreateDirectory(roundDir);
                        }

                        round = round with { FolderPath = roundDir };
                    }
                }
                catch
                {
                    // Ignore folder creation errors for now
                }
            }
        }

        await _localStore.UpsertEQARoundAsync(round);
    }

    public async Task<List<EQARoundResultDto>> GetRoundResultsAsync(Guid roundId)
    {
        return await _localStore.GetEQARoundResultsAsync(roundId);
    }

    public async Task UpsertRoundResultAsync(EQARoundResultDto result)
    {
        await _localStore.UpsertEQARoundResultAsync(result);
    }

    public async Task ApproveRoundAsync(Guid roundId, Guid userId, string userName)
    {
        await _localStore.ApproveRoundAsync(roundId, userId, userName);
    }
}
