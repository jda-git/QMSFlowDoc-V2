using Microsoft.EntityFrameworkCore;
using QMSFlowDoc.Application.Services.Documents;
using QMSFlowDoc.DocumentStorage;
using QMSFlowDoc.Domain.Entities;
using QMSFlowDoc.Infrastructure.Persistence;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Infrastructure.Services.Documents;

/// <summary>
/// Implementación del servicio de gestión de documentos (ISO 15189) mediante EF Core y almacenamiento centralizado
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly QmsDbContext _context;
    private readonly IDocumentStorageService _storageService;

    public DocumentService(QmsDbContext context, IDocumentStorageService storageService)
    {
        _context = context;
        _storageService = storageService;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DocumentDto>> GetDocumentsAsync(bool includeObsolete = false)
    {
        var query = _context.Documents.AsQueryable();
        if (!includeObsolete)
        {
            query = query.Where(d => d.Status != DocumentStatus.OBSOLETE);
        }

        var list = await query
            .Select(d => new
            {
                d.Id,
                d.DocCode,
                d.Title,
                d.FolderId,
                d.DocumentTypeId,
                TypeName = d.DocumentType != null ? d.DocumentType.Name : null,
                d.Area,
                d.Process,
                d.OwnerUserId,
                d.Status,
                ReviewIntervalMonths = d.ReviewIntervalMonths ?? 12,
                d.NextReviewDue,
                d.CreatedAt,
                d.UpdatedAt,
                CurrentVersionLabel = d.Versions.Where(v => v.IsCurrent).Select(v => v.VersionLabel).FirstOrDefault() ?? "1.0"
            })
            .ToListAsync();

        return list.Select(x => new DocumentDto
        {
            Id = x.Id,
            DocCode = x.DocCode,
            Title = x.Title,
            FolderId = x.FolderId,
            DocumentTypeId = x.DocumentTypeId,
            TypeName = x.TypeName,
            Area = x.Area,
            Process = x.Process,
            OwnerUserId = x.OwnerUserId,
            Status = (QMSFlowDoc.Shared.Models.DocumentStatus)x.Status,
            ReviewIntervalMonths = x.ReviewIntervalMonths,
            NextReviewDue = x.NextReviewDue,
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt,
            CurrentVersionLabel = x.CurrentVersionLabel
        }).ToList();
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<DocumentType>> GetDocumentTypesAsync()
    {
        return await _context.DocumentTypes.ToListAsync();
    }

    /// <inheritdoc/>
    public async Task<Document?> GetDocumentByIdAsync(Guid id)
    {
        return await _context.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.Folder)
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    /// <inheritdoc/>
    public async Task<Document?> CreateDocumentAsync(CreateDocumentRequest request, Guid? ownerUserId)
    {
        if (await _context.Documents.AnyAsync(d => d.DocCode == request.DocCode && !d.IsDeleted))
        {
            throw new InvalidOperationException($"Ya existe un documento activo registrado con el código '{request.DocCode}'.");
        }

        var doc = new Document
        {
            Id = Guid.NewGuid(),
            DocCode = request.DocCode,
            Title = request.Title,
            DocumentTypeId = request.DocumentTypeId,
            FolderId = request.FolderId,
            Area = request.Area,
            Process = request.Process,
            OwnerUserId = ownerUserId,
            Status = request.Status.HasValue ? (DocumentStatus)request.Status.Value : DocumentStatus.DRAFT,
            ReviewIntervalMonths = request.ReviewIntervalMonths ?? 12,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (doc.ReviewIntervalMonths > 0)
        {
            doc.NextReviewDue = DateTime.UtcNow.AddMonths(doc.ReviewIntervalMonths.Value);
        }

        _context.Documents.Add(doc);
        await _context.SaveChangesAsync();
        return doc;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateDocumentAsync(Guid id, CreateDocumentRequest request, Guid? userId, string username)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return false;

        if (doc.DocCode != request.DocCode && await _context.Documents.AnyAsync(d => d.DocCode == request.DocCode && d.Id != id && !d.IsDeleted))
        {
            throw new InvalidOperationException($"Ya existe otro documento activo registrado con el código '{request.DocCode}'.");
        }

        doc.DocCode = request.DocCode;
        doc.Title = request.Title;
        doc.DocumentTypeId = request.DocumentTypeId;
        doc.FolderId = request.FolderId;
        doc.Area = request.Area;
        doc.Process = request.Process;
        doc.ReviewIntervalMonths = request.ReviewIntervalMonths ?? doc.ReviewIntervalMonths;
        doc.UpdatedAt = DateTime.UtcNow;

        if (request.Status.HasValue)
        {
            doc.Status = (DocumentStatus)request.Status.Value;
        }

        if (doc.ReviewIntervalMonths.HasValue && doc.ReviewIntervalMonths.Value > 0)
        {
            doc.NextReviewDue = DateTime.UtcNow.AddMonths(doc.ReviewIntervalMonths.Value);
        }

        await LogAuditAsync("EDIT", "Document", id, $"Editó metadatos del documento: '{doc.Title}' ({doc.DocCode})", userId, username);
        return await _context.SaveChangesAsync() > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> UpdateStatusAsync(Guid id, DocumentStatus newStatus, string comments, Guid? userId, string username)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return false;

        var oldStatus = doc.Status;
        doc.Status = newStatus;
        doc.UpdatedAt = DateTime.UtcNow;

        if (newStatus == DocumentStatus.APPROVED)
        {
            var currentVersion = await _context.DocumentVersions
                .Where(v => v.DocumentId == id && v.IsCurrent)
                .FirstOrDefaultAsync();

            if (currentVersion != null)
            {
                currentVersion.ApprovedByUserId = userId;
                currentVersion.ApprovalDate = DateTime.UtcNow;
                currentVersion.EffectiveFrom = DateTime.UtcNow;
            }
        }

        await LogAuditAsync("STATUS_CHANGE", "Document", id, $"Estado cambiado de {oldStatus} a {newStatus}. Motivo: {comments}", userId, username);
        return await _context.SaveChangesAsync() > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteDocumentAsync(Guid id, Guid? userId, string username)
    {
        var doc = await _context.Documents.FindAsync(id);
        if (doc == null) return false;

        doc.IsDeleted = true;
        doc.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("TRASH", "Document", id, $"Envió a la papelera el documento: '{doc.Title}'", userId, username);
        return await _context.SaveChangesAsync() > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> UploadFileAsync(
        Guid id, 
        byte[] fileData, 
        string fileName, 
        string contentType, 
        string versionLabel, 
        string changeSummary, 
        Guid? userId, 
        string username)
    {
        var doc = await _context.Documents
            .Include(d => d.Folder)
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return false;

        if (!Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se permiten archivos .PDF en el gestor documental.");
        }

        // Desmarcar versiones anteriores como actuales
        foreach (var v in doc.Versions)
        {
            v.IsCurrent = false;
        }

        var folderName = doc.Folder?.Name ?? "General";
        using var stream = new MemoryStream(fileData);
        var storageResult = await _storageService.SaveFileAsync(stream, fileName, $"Documentos\\{folderName}");

        int major = 1;
        int minor = 0;
        if (doc.Versions.Any())
        {
            var latest = doc.Versions.OrderByDescending(v => v.CreatedAt).First();
            major = latest.VersionMajor;
            minor = latest.VersionMinor + 1;
        }

        if (!string.IsNullOrWhiteSpace(versionLabel))
        {
            var parts = versionLabel.Replace("v", "").Replace("V", "").Split('.');
            if (parts.Length >= 1 && int.TryParse(parts[0], out int maj))
            {
                major = maj;
                minor = 0;
                if (parts.Length >= 2 && int.TryParse(parts[1], out int min))
                {
                    minor = min;
                }
            }
        }

        var newVersion = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            VersionMajor = major,
            VersionMinor = minor,
            VersionLabel = string.IsNullOrWhiteSpace(versionLabel) ? $"{major}.{minor}" : versionLabel,
            ChangeSummary = changeSummary ?? "Nueva versión cargada",
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow,
            FileName = fileName,
            LocalFilePath = storageResult.RelativePath,
            MimeType = contentType,
            Sha256 = storageResult.Sha256Hash,
            IsCurrent = true
        };

        _context.DocumentVersions.Add(newVersion);
        doc.UpdatedAt = DateTime.UtcNow;

        await LogAuditAsync("UPLOAD", "Document", doc.Id, $"Cargada versión {newVersion.VersionLabel} del archivo: {fileName}", userId, username);
        return await _context.SaveChangesAsync() > 0;
    }

    /// <inheritdoc/>
    public async Task<byte[]?> GetFileContentAsync(Guid id)
    {
        var doc = await _context.Documents
            .Include(d => d.Versions)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (doc == null) return null;

        var currentVersion = doc.Versions.OrderByDescending(v => v.CreatedAt).FirstOrDefault(v => v.IsCurrent);
        if (currentVersion == null || string.IsNullOrEmpty(currentVersion.LocalFilePath))
        {
            return null;
        }

        using var stream = await _storageService.ReadFileAsync(currentVersion.LocalFilePath);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        return ms.ToArray();
    }

    /// <inheritdoc/>
    public async Task<Guid?> GetOrCreateFolderIdAsync(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName)) return null;

        var folder = await _context.Folders.FirstOrDefaultAsync(f => f.Name == folderName);
        if (folder == null)
        {
            folder = new Folder
            {
                Id = Guid.NewGuid(),
                Name = folderName,
                CreatedAt = DateTime.UtcNow
            };
            _context.Folders.Add(folder);
            await _context.SaveChangesAsync();
        }
        return folder.Id;
    }

    private async Task LogAuditAsync(string action, string entityType, Guid? entityId, string details, Guid? userId, string username)
    {
        var audit = new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            UserId = userId ?? Guid.Empty,
            UserName = username ?? "Sistema",
            Timestamp = DateTime.UtcNow,
            MachineName = Environment.MachineName,
            Result = "Success"
        };
        _context.AuditLogs.Add(audit);
    }
}
