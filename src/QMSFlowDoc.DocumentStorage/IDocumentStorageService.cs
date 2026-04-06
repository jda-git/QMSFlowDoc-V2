namespace QMSFlowDoc.DocumentStorage;

/// <summary>
/// Contract for the central document storage service.
/// Handles file operations against the server's document repository.
/// </summary>
public interface IDocumentStorageService
{
    /// <summary>Saves a file to the central repository. Returns the relative path.</summary>
    Task<DocumentStorageResult> SaveFileAsync(Stream fileStream, string fileName, string subfolder, CancellationToken ct = default);

    /// <summary>Opens/reads a file from the central repository.</summary>
    Task<Stream> ReadFileAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Checks if a file exists physically.</summary>
    Task<bool> FileExistsAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Deletes a file (logical — moves to archive).</summary>
    Task<bool> ArchiveFileAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Copies a file to a local temp path for viewing. Returns the local temp path.</summary>
    Task<string> CopyToLocalTempAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Calculates SHA256 hash of a file.</summary>
    Task<string> CalculateHashAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Gets file metadata (size, last modified, etc.)</summary>
    Task<FileMetadata?> GetMetadataAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Creates the initial folder structure on the server.</summary>
    Task CreateFolderStructureAsync(CancellationToken ct = default);
}

/// <summary>Result of a save operation.</summary>
public record DocumentStorageResult(
    string RelativePath,
    string Sha256Hash,
    long FileSizeBytes,
    string MimeType
);

/// <summary>File metadata from the central repository.</summary>
public record FileMetadata(
    string FileName,
    long SizeBytes,
    DateTime LastModifiedUtc,
    string? Sha256Hash
);
