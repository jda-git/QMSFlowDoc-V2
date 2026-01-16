using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Services.Sync;

namespace QMSFlowDoc.Client.Services;

public class SyncAgent
{
    private readonly NetworkSyncService _networkSync;
    private readonly Timer _syncTimer;
    private bool _isSyncing = false;

    public event Action<string>? SyncStatusChanged;

    public SyncAgent(NetworkSyncService networkSync)
    {
        _networkSync = networkSync;
        _networkSync.SyncStatusChanged += (msg) => SyncStatusChanged?.Invoke(msg);
        
        // Run sync every 5 minutes
        _syncTimer = new Timer(async _ => await PerformSyncAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));
    }

    public async Task PerformSyncAsync()
    {
        if (_isSyncing) return;
        _isSyncing = true;
        
        try
        {
            await _networkSync.SyncAllAsync();
        }
        catch (Exception ex)
        {
            SyncStatusChanged?.Invoke($"Error de sincronización: {ex.Message}");
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public void Stop()
    {
        _syncTimer.Dispose();
    }
}
