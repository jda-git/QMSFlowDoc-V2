using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services;

public interface ISearchService
{
    Task<List<SearchResultDto>> SearchAsync(string query);
}

/// <summary>
/// V2: Global search across multiple tables using SQL Server LIKE.
/// Future: Could use SQL Server Full-Text Search for better performance.
/// </summary>
public class SearchService : ISearchService
{
    private readonly ClientDbContextFactory _dbFactory;

    public SearchService(ClientDbContextFactory dbFactory) => _dbFactory = dbFactory;

    public async Task<List<SearchResultDto>> SearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return new List<SearchResultDto>();

        var results = new List<SearchResultDto>();
        var pattern = $"%{query}%";

        using var ctx = _dbFactory.CreateContext();

        // Documents
        var docs = await ctx.Documents
            .Where(d => EF.Functions.Like(d.Title, pattern) || EF.Functions.Like(d.DocCode, pattern))
            .Take(10)
            .Select(d => new SearchResultDto(d.Id, "Documento", d.Title, d.DocCode, "documentos"))
            .ToListAsync();
        results.AddRange(docs);

        // Equipment
        var equip = await ctx.Equipments
            .Where(e => EF.Functions.Like(e.Name, pattern) || EF.Functions.Like(e.SerialNumber!, pattern))
            .Take(10)
            .Select(e => new SearchResultDto(e.Id, "Equipo", e.Name, e.SerialNumber ?? "", "equipos"))
            .ToListAsync();
        results.AddRange(equip);

        // Reagents
        var reags = await ctx.Reagents
            .Where(r => EF.Functions.Like(r.Name, pattern) || EF.Functions.Like(r.Reference, pattern))
            .Take(10)
            .Select(r => new SearchResultDto(r.Id, "Reactivo", r.Name, r.Reference, "inventario"))
            .ToListAsync();
        results.AddRange(reags);

        // Nonconformities
        var ncs = await ctx.Nonconformities
            .Where(nc => EF.Functions.Like(nc.Title, pattern))
            .Take(10)
            .Select(nc => new SearchResultDto(nc.Id, "No Conformidad", nc.Title, nc.Status.ToString(), "incidencias"))
            .ToListAsync();
        results.AddRange(ncs);

        // Staff
        var staff = await ctx.StaffProfiles
            .Include(s => s.User)
            .Where(s => s.User != null && (EF.Functions.Like(s.User.FullName, pattern) || EF.Functions.Like(s.PositionTitle!, pattern)))
            .Take(10)
            .Select(s => new SearchResultDto(s.Id, "Personal", s.User!.FullName, s.PositionTitle ?? "", "personal"))
            .ToListAsync();
        results.AddRange(staff);

        return results;
    }
}
