using Microsoft.Extensions.Logging;
using QMSFlowDoc.DocumentStorage.Helpers;

namespace QMSFlowDoc.DocumentStorage;

/// <summary>
/// Central document storage service that manages files on the server's
/// shared folder (UNC path). All operations use relative paths stored in DB;
/// the base path is resolved at runtime from configuration.
///
/// File lifecycle:
///   1. Client calls SaveFileAsync → stream written via SafeFileWriter
///   2. SHA256 hash computed and returned
///   3. DB stores only the relative path + metadata
///   4. Client reads via ReadFileAsync or CopyToLocalTempAsync
///   5. Old versions are never overwritten (immutable once saved)
/// </summary>
public class CentralDocumentStorageService : IDocumentStorageService
{
    private readonly string _basePath;
    private readonly ILogger<CentralDocumentStorageService> _logger;

    /// <summary>
    /// Standard subfolder structure created under the base path.
    /// </summary>
    private static readonly string[] FolderStructure =
    [
        "Documentos",
        "Documentos\\Borradores",
        "Documentos\\Aprobados",
        "Documentos\\Obsoletos",
        "Documentos\\Versiones",
        "Adjuntos",
        "Informes",
        "Certificados",
        "Manuales",
        "Logs",
        "Temp"
    ];

    public CentralDocumentStorageService(string basePath, ILogger<CentralDocumentStorageService> logger)
    {
        if (string.IsNullOrWhiteSpace(basePath))
            throw new ArgumentException("Document storage base path cannot be empty.", nameof(basePath));

        _basePath = basePath;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<DocumentStorageResult> SaveFileAsync(
        Stream fileStream, string fileName, string subfolder, CancellationToken ct = default)
    {
        if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
        if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("File name is required.", nameof(fileName));

        // Sanitize the file name to prevent path traversal
        var safeFileName = SanitizeFileName(fileName);
        var safeSubfolder = SanitizePath(subfolder);

        // Generate a unique name to avoid collisions: {timestamp}_{guid}_{filename}
        var uniqueName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}_{safeFileName}";
        var relativePath = Path.Combine(safeSubfolder, uniqueName);
        var fullPath = Path.Combine(_basePath, relativePath);

        _logger.LogInformation("Saving file: {RelativePath}", relativePath);

        try
        {
            // Step 1: Compute hash before writing
            var hash = await FileHashHelper.ComputeSha256Async(fileStream, ct);

            // Step 2: Safe write (temp → rename)
            var fileSize = await SafeFileWriter.WriteAsync(fileStream, fullPath, ct);

            // Step 3: Verify hash of written file
            var verifyHash = await FileHashHelper.ComputeSha256Async(fullPath, ct);
            if (hash != verifyHash)
            {
                // Integrity failure — delete and throw
                File.Delete(fullPath);
                throw new IOException($"File integrity check failed for {relativePath}. Expected {hash}, got {verifyHash}.");
            }

            // Step 4: Determine MIME type
            var mimeType = GetMimeType(safeFileName);

            _logger.LogInformation("File saved successfully: {RelativePath} ({Size} bytes, SHA256={Hash})",
                relativePath, fileSize, hash);

            return new DocumentStorageResult(relativePath, hash, fileSize, mimeType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file: {FileName} to {Subfolder}", fileName, subfolder);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<Stream> ReadFileAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found in repository: {relativePath}", relativePath);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public Task<bool> FileExistsAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        return Task.FromResult(File.Exists(fullPath));
    }

    /// <inheritdoc/>
    public Task<bool> ArchiveFileAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            return Task.FromResult(false);

        try
        {
            // Move to an _archived subfolder next to the original
            var dir = Path.GetDirectoryName(fullPath) ?? _basePath;
            var archiveDir = Path.Combine(dir, "_archived");
            Directory.CreateDirectory(archiveDir);

            var archivePath = Path.Combine(archiveDir, Path.GetFileName(fullPath));
            File.Move(fullPath, archivePath, overwrite: false);

            _logger.LogInformation("File archived: {RelativePath} → {ArchivePath}", relativePath, archivePath);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to archive file: {RelativePath}", relativePath);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc/>
    public async Task<string> CopyToLocalTempAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found in repository: {relativePath}", relativePath);

        var tempDir = Path.Combine(Path.GetTempPath(), "QMSFlowDoc");
        Directory.CreateDirectory(tempDir);

        var tempPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}_{Path.GetFileName(fullPath)}");

        using var source = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        using var dest = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, useAsync: true);
        await source.CopyToAsync(dest, ct);

        _logger.LogDebug("File copied to local temp: {TempPath}", tempPath);
        return tempPath;
    }

    /// <inheritdoc/>
    public async Task<string> CalculateHashAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"File not found: {relativePath}", relativePath);

        return await FileHashHelper.ComputeSha256Async(fullPath, ct);
    }

    /// <inheritdoc/>
    public Task<FileMetadata?> GetMetadataAsync(string relativePath, CancellationToken ct = default)
    {
        var fullPath = ResolvePath(relativePath);
        var info = new FileInfo(fullPath);

        if (!info.Exists)
            return Task.FromResult<FileMetadata?>(null);

        return Task.FromResult<FileMetadata?>(new FileMetadata(
            info.Name,
            info.Length,
            info.LastWriteTimeUtc,
            null // Hash computed on demand
        ));
    }

    /// <inheritdoc/>
    public Task CreateFolderStructureAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Creating folder structure at: {BasePath}", _basePath);

        foreach (var folder in FolderStructure)
        {
            var fullPath = Path.Combine(_basePath, folder);
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _logger.LogInformation("  Created: {Folder}", folder);
            }
        }

        _logger.LogInformation("Folder structure creation complete.");
        return Task.CompletedTask;
    }

    // ─── Private Helpers ───────────────────────────────────────

    private string ResolvePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("Relative path cannot be empty.", nameof(relativePath));

        // Prevent path traversal attacks
        var normalized = Path.GetFullPath(Path.Combine(_basePath, relativePath));
        if (!normalized.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException($"Path traversal detected: {relativePath}");

        return normalized;
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalid.Contains(c)).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "unnamed" : sanitized;
    }

    private static string SanitizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "Documentos";
        var invalid = Path.GetInvalidPathChars();
        var sanitized = new string(path.Where(c => !invalid.Contains(c)).ToArray());
        // Prevent going up directories
        sanitized = sanitized.Replace("..", "");
        return string.IsNullOrWhiteSpace(sanitized) ? "Documentos" : sanitized;
    }

    private static string GetMimeType(string fileName)
    {
        var ext = Path.GetExtension(fileName)?.ToLowerInvariant();
        return ext switch
        {
            ".pdf" => "application/pdf",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xls" => "application/vnd.ms-excel",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".ppt" => "application/vnd.ms-powerpoint",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".txt" => "text/plain",
            ".csv" => "text/csv",
            ".xml" => "application/xml",
            ".json" => "application/json",
            ".zip" => "application/zip",
            ".html" or ".htm" => "text/html",
            _ => "application/octet-stream"
        };
    }
}
