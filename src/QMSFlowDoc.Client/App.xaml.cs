using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;

namespace QMSFlowDoc.Client;

public partial class App : Application
{
    public Window? Window { get; private set; }

    // ── V2: Central database factory ──
    public Services.ClientDbContextFactory DbFactory { get; }

    // ── Services (V2: all use DbFactory) ──
    public Services.IAuthService AuthService { get; }
    public Services.IDocumentService DocumentService { get; }
    public Services.IInventoryService InventoryService { get; }
    public Services.IEquipmentService EquipmentService { get; }
    public Services.IStaffService StaffService { get; }
    public Services.IQualityService QualityService { get; }
    public Services.IImprovementService ImprovementService { get; }
    public Services.IDashboardService DashboardService { get; }
    public Services.ISearchService SearchService { get; }
    public Services.IFolderService FolderService { get; }
    public Services.IConfigurationService ConfigurationService { get; }
    public Services.ITrainingService TrainingService { get; }
    public Services.ICompetencyService CompetencyService { get; }
    public Services.IAuthorizationService AuthorizationService { get; }
    public Services.IPermissionsService PermissionsService { get; }
    public Services.IPrintingService PrintingService { get; } = new Services.PrintingService();
    public Services.IEQAService EQAService { get; }
    public Services.IMethodService MethodService { get; }
    public Services.IExportService ExportService { get; }
    public Services.ISupplierService SupplierService { get; }
    public Services.IAuditService AuditService { get; }

    // ── Document Management (kept as-is, no DB dependency) ──
    public Services.Documents.PdfWatermarkService PdfWatermarkService { get; }
    public Services.Documents.DocumentRenderer DocumentRenderer { get; }
    public Services.Documents.PrintControlService PrintControlService { get; }

    // ── Legacy references (kept temporarily for compatibility) ──
    public Services.ILocalCacheService LocalCacheService { get; } = new Services.LocalCacheService();
    public Services.LocalDocumentStore LocalStore { get; }
    public Services.NetworkConfigStore NetworkConfigStore { get; } = new Services.NetworkConfigStore();
    public Services.LocalConfigStore LocalConfigStore { get; } = new Services.LocalConfigStore();
    public Services.AuditLogger EquipmentAuditLogger { get; }

    // Legacy sync (will be removed once migration is complete)
    public Services.Sync.SnapshotStore SnapshotStore { get; }
    public Services.Sync.SyncLogger SyncLogger { get; }
    public Services.Sync.AuditLogger AuditLogger { get; }
    public Services.Storage.NetworkStorageProvider? RemoteProvider { get; private set; }
    public Services.Sync.SyncEngine? RemoteSyncEngine { get; private set; }

    public static Microsoft.UI.Xaml.Window? MainWindowInstance { get; set; }
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        this.InitializeComponent();

        // ═══════════════════════════════════════════
        // V2: Initialize central database factory
        // ═══════════════════════════════════════════
        DbFactory = new Services.ClientDbContextFactory();

        // Load client settings and configure factory
        var clientSettings = Client.Services.ClientSettingsService.Load();
        if (clientSettings.IsConfigured)
        {
            DbFactory.Configure(clientSettings);
        }

        // ═══════════════════════════════════════════
        // V2: Create all services from DbFactory
        // ═══════════════════════════════════════════
        AuthService = new Services.AuthService(DbFactory);
        EquipmentService = new Services.EquipmentService(DbFactory);
        InventoryService = new Services.InventoryService(DbFactory);
        QualityService = new Services.QualityService(DbFactory);
        DashboardService = new Services.DashboardService(DbFactory);
        SearchService = new Services.SearchService(DbFactory);
        EQAService = new Services.EQAService(DbFactory);
        MethodService = new Services.MethodService(DbFactory);
        SupplierService = new Services.SupplierService(DbFactory);
        AuditService = new Services.AuditService(DbFactory);
        PermissionsService = new Services.PermissionsService(DbFactory);

        // ═══════════════════════════════════════════
        // Services still using legacy pattern (to be migrated)
        // ═══════════════════════════════════════════
        var httpClient = new HttpClient { BaseAddress = new Uri("http://offline-mode-active/api/") };
        LocalStore = new Services.LocalDocumentStore(NetworkConfigStore);

        DocumentService = new Services.DocumentService(httpClient, LocalCacheService, LocalStore, NetworkConfigStore, AuthService);
        StaffService = new Services.StaffService(httpClient, null, NetworkConfigStore);
        ImprovementService = new Services.ImprovementService(httpClient, NetworkConfigStore);
        FolderService = new Services.FolderService(httpClient, null, NetworkConfigStore);
        ConfigurationService = new Services.ConfigurationService(httpClient, NetworkConfigStore);
        TrainingService = new Services.TrainingService(httpClient, null, NetworkConfigStore);
        CompetencyService = new Services.CompetencyService(NetworkConfigStore);
        AuthorizationService = new Services.AuthorizationService(httpClient, NetworkConfigStore);
        ExportService = new Services.ExportService();
        EquipmentAuditLogger = new Services.AuditLogger(ConfigurationService, NetworkConfigStore);

        // Register in DI shim
        var services = new ServiceCollection();
        services.AddSingleton(AuthService);
        services.AddSingleton(DocumentService);
        services.AddSingleton(AuditService);
        Services = services.BuildServiceProvider();

        // Init Sync (legacy)
        SnapshotStore = new Services.Sync.SnapshotStore();
        SyncLogger = new Services.Sync.SyncLogger();
        AuditLogger = new Services.Sync.AuditLogger();

        PdfWatermarkService = new Services.Documents.PdfWatermarkService();
        DocumentRenderer = new Services.Documents.DocumentRenderer(PdfWatermarkService, AuditLogger);
        PrintControlService = new Services.Documents.PrintControlService(PdfWatermarkService, AuditLogger);
    }

    public MainWindow? MainWindow => Window as MainWindow;

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBox(IntPtr hWnd, String text, String caption, uint type);

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            this.UnhandledException += (s, e) =>
            {
                e.Handled = true;
                MessageBox(IntPtr.Zero, $"Unhandled Error: {e.Exception.Message}\n\n{e.Exception.StackTrace}", "Critical Error", 0x10);
            };

            // ═══════════════════════════════════════════
            // V2: Check if client is configured
            // ═══════════════════════════════════════════
            if (!DbFactory.IsConfigured)
            {
                // Show connection setup dialog first
                // For now, fall through to legacy initialization
            }

            // Legacy initialization (for services not yet migrated)
            await LocalCacheService.InitializeAsync();
            await SnapshotStore.InitializeAsync();
            await PrintControlService.InitializeAsync();
            await LocalStore.InitializeAsync();

            // V2: Seed permissions if DB is available
            if (DbFactory.IsConfigured)
            {
                try { await PermissionsService.EnsureSeedDataAsync(); } catch { /* DB not ready yet */ }
            }

            await DocumentService.InitializeAsync();

            // Initialize network paths (legacy)
            try
            {
                if (await NetworkConfigStore.ValidatePathsAsync())
                {
                    await NetworkConfigStore.InitializeStructureAsync();
                    var config = await NetworkConfigStore.LoadAsync();
                    if (!string.IsNullOrEmpty(config.LocalBasePath))
                        SyncLogger.SetBasePath(config.LocalBasePath);

                    if (!string.IsNullOrEmpty(config.NetworkBasePath) && !string.IsNullOrEmpty(config.LocalBasePath))
                    {
                        RemoteProvider = new Services.Storage.NetworkStorageProvider(config.NetworkBasePath);
                        RemoteSyncEngine = new Services.Sync.SyncEngine(SnapshotStore, NetworkConfigStore, SyncLogger, AuditLogger);
                        RemoteSyncEngine.MasterRootId = string.Empty;
                    }
                }
            }
            catch { /* Not configured */ }

            Window = new MainWindow();
            MainWindowInstance = Window;

            if (!AuthService.IsAuthenticated)
                NavigateToLogin();
            else
            {
                NavigateToMain();
                await CheckAndRunStartupSyncAsync();
            }

            Window.Activate();
        }
        catch (Exception ex)
        {
            MessageBox(IntPtr.Zero, $"Startup Error: {ex.Message}\n\n{ex.StackTrace}", "Fatal Startup Error", 0x10);
        }
    }

    public void NavigateToLogin()
    {
        if (Window is MainWindow mw) mw.ShowLogin();
    }

    public void NavigateToMain()
    {
        if (Window is MainWindow mw) mw.ShowMain();
    }

    private async System.Threading.Tasks.Task CheckAndRunStartupSyncAsync()
    {
        try
        {
            if (RemoteSyncEngine == null) return;
            if (!await NetworkConfigStore.ValidatePathsAsync()) return;
            var config = await NetworkConfigStore.LoadAsync();
            if (!config.AutoSyncOnStartup) return;
            await RemoteSyncEngine.RunSyncAsync();
        }
        catch (Exception ex)
        {
            await SyncLogger.LogErrorAsync("Startup sync failed", ex);
        }
    }
}
