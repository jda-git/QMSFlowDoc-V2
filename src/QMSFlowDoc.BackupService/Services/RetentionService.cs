using Microsoft.Extensions.Logging;

namespace QMSFlowDoc.BackupService.Services;

/// <summary>
/// Cleans up old backups based on the retention policy (days).
/// </summary>
public class RetentionService
{
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(ILogger<RetentionService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Deletes backup folders and files older than retentionDays.
    /// </summary>
    public Task CleanupAsync(string backupBasePath, int retentionDays, CancellationToken ct = default)
    {
        var cutoff = DateTime.Now.AddDays(-retentionDays);
        _logger.LogInformation("Running retention cleanup: deleting backups older than {Cutoff:d} ({Days} days)", cutoff, retentionDays);

        int deletedDirs = 0, deletedFiles = 0;

        // Clean DB backups
        var dbDir = Path.Combine(backupBasePath, "DB");
        if (Directory.Exists(dbDir))
        {
            foreach (var file in Directory.GetFiles(dbDir, "*.bak"))
            {
                ct.ThrowIfCancellationRequested();
                var fi = new FileInfo(file);
                if (fi.CreationTime < cutoff)
                {
                    try
                    {
                        fi.Delete();
                        deletedFiles++;
                        _logger.LogDebug("Deleted old DB backup: {File}", fi.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old backup: {File}", fi.FullName);
                    }
                }
            }
        }

        // Clean File backups (timestamped folders)
        var filesDir = Path.Combine(backupBasePath, "Files");
        if (Directory.Exists(filesDir))
        {
            foreach (var dir in Directory.GetDirectories(filesDir))
            {
                ct.ThrowIfCancellationRequested();
                var di = new DirectoryInfo(dir);
                if (di.CreationTime < cutoff)
                {
                    try
                    {
                        di.Delete(recursive: true);
                        deletedDirs++;
                        _logger.LogDebug("Deleted old file backup: {Dir}", di.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete old backup dir: {Dir}", di.FullName);
                    }
                }
            }
        }

        _logger.LogInformation("Retention cleanup complete: {Files} DB files, {Dirs} file backups deleted", deletedFiles, deletedDirs);
        return Task.CompletedTask;
    }
}
