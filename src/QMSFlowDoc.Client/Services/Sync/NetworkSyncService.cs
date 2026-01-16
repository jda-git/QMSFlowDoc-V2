using System;
using System.IO;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Services.Storage;

namespace QMSFlowDoc.Client.Services.Sync;

public class NetworkSyncService
{
    private readonly NetworkConfigStore _configStore;
    private readonly string _localDbPath;
    private readonly string _localFilesPath;

    public event Action<string>? SyncStatusChanged;

    public NetworkSyncService(NetworkConfigStore configStore, string localDbPath, string localFilesPath)
    {
        _configStore = configStore;
        _localDbPath = localDbPath;
        _localFilesPath = localFilesPath;
    }

    public async Task SyncAllAsync()
    {
        try
        {
            var config = await _configStore.LoadAsync();
            if (string.IsNullOrEmpty(config.NetworkBasePath) || string.IsNullOrEmpty(config.LocalBasePath))
            {
                SyncStatusChanged?.Invoke("Sync omitido: Rutas no configuradas.");
                return;
            }

            SyncStatusChanged?.Invoke("Conectando a red...");
            
            var networkProvider = new NetworkStorageProvider(config.NetworkBasePath);

            if (!await networkProvider.TestConnectionAsync())
            {
                SyncStatusChanged?.Invoke("Error: No se puede acceder a la ruta de red.");
                return;
            }

            // 1. Sync Database
            await SyncDatabaseAsync(networkProvider);

            // 2. Sync Files
            await SyncFilesAsync(networkProvider);

            SyncStatusChanged?.Invoke("Sincronización finalizada.");
        }
        catch (Exception ex)
        {
            SyncStatusChanged?.Invoke($"Error fatal en sync: {ex.Message}");
        }
    }

    private async Task SyncDatabaseAsync(NetworkStorageProvider networkProvider)
    {
        SyncStatusChanged?.Invoke("Sincronizando base de datos...");
        
        string dbName = "qmsflowdoc.db";
        string localDb = Path.Combine(_localDbPath, dbName);
        string networkDbPath = "Data"; // Relative to Network Base
        
        // Ensure remote Data folder exists
        await networkProvider.CreateDirectoryAsync(networkDbPath);
        
        // Check remote DB
        var remoteDbMetadata = await networkProvider.GetMetadataAsync(Path.Combine(networkDbPath, dbName));
        
        if (!File.Exists(localDb))
        {
             // If local doesn't exist but remote does, download it
             if (remoteDbMetadata != null)
             {
                 SyncStatusChanged?.Invoke("Descargando base de datos remota...");
                 await networkProvider.ReadFileAsync(remoteDbMetadata.RelativePath).ContinueWith(async t => 
                 {
                     using var fs = new FileStream(localDb, FileMode.Create);
                     await t.Result.CopyToAsync(fs);
                 });
             }
             return;
        }

        if (remoteDbMetadata == null)
        {
            // Remote doesn't exist, upload local
            SyncStatusChanged?.Invoke("Subiendo base de datos local...");
            await networkProvider.CopyFileAsync(localDb, Path.Combine(networkDbPath, dbName), overwrite: true);
            return;
        }

        // Both exist, Last Write Wins
        FileInfo localInfo = new FileInfo(localDb);
        // Add a tolerance of 2 seconds for difference
        if (remoteDbMetadata.ModifiedTimeUtc > localInfo.LastWriteTimeUtc.AddSeconds(5))
        {
            // Remote is newer, download
             SyncStatusChanged?.Invoke("Actualizando BD local desde red...");
             // Close any connections? Theoretically SQLite handles this but better to be safe in real app.
             // For now we assume file lock might issue, user should "close" app or we implement backup restore logic.
             // Accessing file directly while App is running might fail if locked by SQLite WAL.
             // We'll attempt copy.
             try 
             {
                 var stream = await networkProvider.ReadFileAsync(remoteDbMetadata.RelativePath);
                 using var fs = new FileStream(localDb, FileMode.Create, FileAccess.Write, FileShare.None); 
                 await stream.CopyToAsync(fs);
             }
             catch (IOException)
             {
                 SyncStatusChanged?.Invoke("Advertencia: BD bloqueada, no se pudo actualizar.");
             }
        }
        else if (localInfo.LastWriteTimeUtc > remoteDbMetadata.ModifiedTimeUtc.AddSeconds(5))
        {
            // Local is newer, upload locally
             SyncStatusChanged?.Invoke("Subiendo cambios de BD a red...");
             await networkProvider.CopyFileAsync(localDb, Path.Combine(networkDbPath, dbName), overwrite: true);
        }
    }

    private async Task SyncFilesAsync(NetworkStorageProvider networkProvider)
    {
         SyncStatusChanged?.Invoke("Sincronizando archivos...");
         // Simple one-way or two-way sync for "Documents" folder
         // Implementation omitted for brevity in first pass, can rely on SyncEngine logic adapted later
         // or simple recursion here.
         
         // For now, let's just ensure folders exist
         await networkProvider.CreateDirectoryAsync("Documents");
    }
}
