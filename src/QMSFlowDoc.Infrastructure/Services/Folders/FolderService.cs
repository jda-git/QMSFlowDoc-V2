using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Folders;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Infrastructure.Services.Folders;

/// <summary>
/// Implementación del servicio de carpetas del explorador documental mediante EF Core
/// </summary>
public class FolderService : IFolderService
{
    private readonly QmsDbContext _context;

    public FolderService(QmsDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FolderDto>> GetFoldersAsync(Guid? parentId = null)
    {
        return await _context.Folders
            .Where(f => f.ParentFolderId == parentId)
            .Select(f => new FolderDto
            {
                Id = f.Id,
                Name = f.Name,
                ParentFolderId = f.ParentFolderId,
                SubFolderCount = _context.Folders.Count(sf => sf.ParentFolderId == f.Id),
                DocumentCount = _context.Documents.Count(d => d.FolderId == f.Id && !d.IsDeleted)
            })
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<FolderDto>> GetAllFoldersAsync()
    {
        return await _context.Folders
            .Select(f => new FolderDto
            {
                Id = f.Id,
                Name = f.Name,
                ParentFolderId = f.ParentFolderId,
                SubFolderCount = _context.Folders.Count(sf => sf.ParentFolderId == f.Id),
                DocumentCount = _context.Documents.Count(d => d.FolderId == f.Id && !d.IsDeleted)
            })
            .ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<bool> CreateFolderAsync(string name, Guid? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la carpeta no puede estar vacío.", nameof(name));

        var folder = new Folder
        {
            Id = Guid.NewGuid(),
            Name = name,
            ParentFolderId = parentId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Folders.Add(folder);
        return await _context.SaveChangesAsync() > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> RenameFolderAsync(Guid id, string newName, Guid? parentId = null)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("El nuevo nombre de la carpeta no puede estar vacío.", nameof(newName));

        var folder = await _context.Folders.FindAsync(id);
        if (folder == null) return false;

        folder.Name = newName;
        folder.ParentFolderId = parentId;
        return await _context.SaveChangesAsync() > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteFolderAsync(Guid id)
    {
        var folder = await _context.Folders.FindAsync(id);
        if (folder == null) return false;

        // Comprobación de seguridad: evitar huérfanos
        var hasDocs = await _context.Documents.AnyAsync(d => d.FolderId == id && !d.IsDeleted);
        var hasSubfolders = await _context.Folders.AnyAsync(sf => sf.ParentFolderId == id);
        
        if (hasDocs || hasSubfolders)
        {
            throw new InvalidOperationException("No se puede eliminar una carpeta que contiene documentos o subcarpetas activas.");
        }

        _context.Folders.Remove(folder);
        return await _context.SaveChangesAsync() > 0;
    }
}
