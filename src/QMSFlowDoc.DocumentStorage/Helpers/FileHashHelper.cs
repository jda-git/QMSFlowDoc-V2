using System.Security.Cryptography;

namespace QMSFlowDoc.DocumentStorage.Helpers;

/// <summary>
/// Computes SHA256 hashes for file integrity verification.
/// </summary>
public static class FileHashHelper
{
    /// <summary>
    /// Computes SHA256 hash of a file given its full path.
    /// </summary>
    public static async Task<string> ComputeSha256Async(string filePath, CancellationToken ct = default)
    {
        using var sha256 = SHA256.Create();
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Computes SHA256 hash from a stream. Resets stream position to beginning afterward.
    /// </summary>
    public static async Task<string> ComputeSha256Async(Stream stream, CancellationToken ct = default)
    {
        using var sha256 = SHA256.Create();
        stream.Position = 0;
        var hashBytes = await sha256.ComputeHashAsync(stream, ct);
        stream.Position = 0;
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
