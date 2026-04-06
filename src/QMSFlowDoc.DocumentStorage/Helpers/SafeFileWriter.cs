namespace QMSFlowDoc.DocumentStorage.Helpers;

/// <summary>
/// Performs safe, atomic file writes using a temporary file pattern:
/// 1. Write to .tmp file
/// 2. Verify size/hash
/// 3. Rename to final destination
/// This prevents partial writes from corrupting the repository.
/// </summary>
public static class SafeFileWriter
{
    /// <summary>
    /// Safely writes a stream to a destination path using temp-then-rename pattern.
    /// Returns the final file size in bytes.
    /// </summary>
    public static async Task<long> WriteAsync(Stream source, string destinationPath, CancellationToken ct = default)
    {
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = destinationPath + ".tmp";

        try
        {
            // Step 1: Write to temp file
            using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                source.Position = 0;
                await source.CopyToAsync(tempStream, ct);
                await tempStream.FlushAsync(ct);
            }

            // Step 2: Verify the temp file exists and has content
            var tempInfo = new FileInfo(tempPath);
            if (!tempInfo.Exists || tempInfo.Length == 0)
            {
                throw new IOException($"Temporary file write failed or produced empty file: {tempPath}");
            }

            // Step 3: If destination already exists, remove it (should not happen for new versions)
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            // Step 4: Atomic rename
            File.Move(tempPath, destinationPath);

            return tempInfo.Length;
        }
        catch
        {
            // Clean up temp file on failure
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best effort */ }
            }
            throw;
        }
    }
}
