using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Services.Sync;

public class SyncEngine
{
    private readonly SnapshotStore _store;
    private readonly NetworkSyncService _networkSyncService;
    private readonly ISyncLogger _logger;
    private readonly IAuditLogger _audit;

    public event Action<string>? SyncStatusChanged;
    
    public string? MasterRootId { get; set; }
    
    public SnapshotStore GetSnapshotStore() => _store;

    public SyncEngine(
        SnapshotStore store, 
        NetworkConfigStore configStore, 
        ISyncLogger logger, 
        IAuditLogger audit)
    {
        _store = store;
        _logger = logger;
        _audit = audit;
        
        // Instantiate dependencies for NetworkSyncService
        var conflictResolver = new ConflictResolver(store, audit);
        _networkSyncService = new NetworkSyncService(configStore, logger, conflictResolver, store, audit);
        _networkSyncService.SyncStatusChanged += (msg) => SyncStatusChanged?.Invoke(msg);
    }

    public async Task RunSyncAsync()
    {
        await LogAsync("Iniciando sincronización (Mirror Mode)...");
        try 
        {
            // Execute Mirror Sync logic via NetworkSyncService
            bool restartRequired = await _networkSyncService.PerformMirrorSync();
            
            if (restartRequired)
            {
                await LogAsync("⚠️ ACTUALIZACIÓN CRÍTICA: La base de datos ha sido actualizada. Reiniciando aplicación...");
                await Task.Delay(2000); // Give time to log
                
                // Force Restart
                // Microsoft.Windows.AppLifecycle.AppInstance.Restart(""); // Windows App SDK 1.0+ method
                // Or simplified fallback for now:
                System.Diagnostics.Process.Start(System.Environment.ProcessPath!);
                System.Environment.Exit(0);
            }
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync("Sync Failed", ex);
            SyncStatusChanged?.Invoke("Error de sincronización.");
        }
    }

    // Legacy helper kept for potential compilation compatibility, but unused logic
    private async Task LogAsync(string msg)
    {
        await _logger.LogAsync(msg);
        SyncStatusChanged?.Invoke(msg);
    }




}
