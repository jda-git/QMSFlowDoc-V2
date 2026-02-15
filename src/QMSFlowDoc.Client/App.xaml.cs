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
    public Services.IAuthService AuthService { get; }
    public Services.ILocalCacheService LocalCacheService { get; } = new Services.LocalCacheService();
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
    
    // Sync Infrastructure
    public Services.Sync.SnapshotStore SnapshotStore { get; }
    public Services.Storage.NetworkStorageProvider? RemoteProvider { get; private set; }
    public Services.Sync.SyncEngine? RemoteSyncEngine { get; private set; }
    public Services.Sync.SyncLogger SyncLogger { get; }
    public Services.Sync.AuditLogger AuditLogger { get; }
    
    // Document Management Services
    public Services.Documents.PdfWatermarkService PdfWatermarkService { get; }
    public Services.Documents.DocumentRenderer DocumentRenderer { get; }
    public Services.Documents.PrintControlService PrintControlService { get; }
    
    // Configuration & Audit
    public Services.AuditLogger EquipmentAuditLogger { get; }
    public Services.NetworkConfigStore NetworkConfigStore { get; } = new Services.NetworkConfigStore();
    public Services.LocalConfigStore LocalConfigStore { get; } = new Services.LocalConfigStore();
    
    // Core Data Store
    public Services.LocalDocumentStore LocalStore { get; }

    public Services.IEQAService EQAService { get; }
    public Services.IMethodService MethodService { get; }
    public Services.IExportService ExportService { get; }
    public Services.ISupplierService SupplierService { get; }

    public static Microsoft.UI.Xaml.Window? MainWindowInstance { get; set; }




    public Services.IAuditService AuditService { get; }

    // Service Locator Shim
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        this.InitializeComponent();
        
        // Base API address (Optional in Local-First mode; services will fallback to SQLite if unavailable)
        var httpClient = new HttpClient { BaseAddress = new Uri("http://offline-mode-active/api/") };
        
        // Init Shared Stores
        LocalStore = new Services.LocalDocumentStore(NetworkConfigStore);

        AuthService = new Services.AuthService(httpClient);
        DocumentService = new Services.DocumentService(httpClient, LocalCacheService, LocalStore, NetworkConfigStore, AuthService);
        InventoryService = new Services.InventoryService(httpClient, null, NetworkConfigStore);
        EquipmentService = new Services.EquipmentService(httpClient, null, NetworkConfigStore);
        StaffService = new Services.StaffService(httpClient, null, NetworkConfigStore);
        QualityService = new Services.QualityService(httpClient, null, NetworkConfigStore);
        ImprovementService = new Services.ImprovementService(httpClient, NetworkConfigStore);
        DashboardService = new Services.DashboardService(httpClient, null, NetworkConfigStore);
        SearchService = new Services.SearchService(httpClient, null, NetworkConfigStore);
        FolderService = new Services.FolderService(httpClient, null, NetworkConfigStore);
        ConfigurationService = new Services.ConfigurationService(httpClient, NetworkConfigStore);
        TrainingService = new Services.TrainingService(httpClient, null, NetworkConfigStore);
        CompetencyService = new Services.CompetencyService(NetworkConfigStore);
        AuthorizationService = new Services.AuthorizationService(httpClient, NetworkConfigStore);
        EQAService = new Services.EQAService(LocalStore);
        MethodService = new Services.MethodService(LocalStore);
        ExportService = new Services.ExportService();
        EquipmentAuditLogger = new Services.AuditLogger(ConfigurationService, NetworkConfigStore);
        AuditService = new Services.AuditService(LocalStore); // Initialize AuditService
        SupplierService = new Services.SupplierService(LocalStore); // ISO 15189:2022 Section 6.8
        PermissionsService = new Services.PermissionsService(LocalStore);

        // Register in shim
        var services = new ServiceCollection();
        services.AddSingleton(AuthService);
        services.AddSingleton(DocumentService);
        services.AddSingleton(AuditService); 
        Services = services.BuildServiceProvider();

        
        // Init Sync Infrastructure
        SnapshotStore = new Services.Sync.SnapshotStore();
        SyncLogger = new Services.Sync.SyncLogger();
        AuditLogger = new Services.Sync.AuditLogger();
        
        // Init Document Management Services
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

            await LocalCacheService.InitializeAsync();
            await SnapshotStore.InitializeAsync();
            await PrintControlService.InitializeAsync(); // Init PrintLog database
            
            // Initialize Core Store explicitly
            await LocalStore.InitializeAsync();
            await PermissionsService.EnsureSeedDataAsync();

            // Initialize LocalDocumentStore if using local mode
            // Initialize LocalDocumentStore via Service (explicit call)
            await DocumentService.InitializeAsync();
            
            // Initialize folder structure if configured
            try
            {
                if (await NetworkConfigStore.ValidatePathsAsync())
                {
                    await NetworkConfigStore.InitializeStructureAsync();
                    
                    var config = await NetworkConfigStore.LoadAsync();
                    // Set sync logger to use the configured local path
                    if (!string.IsNullOrEmpty(config.LocalBasePath))
                    {
                        SyncLogger.SetBasePath(config.LocalBasePath);
                    }
                    
                        // Initialize Remote Provider and Sync Engine
                        if (!string.IsNullOrEmpty(config.NetworkBasePath) && !string.IsNullOrEmpty(config.LocalBasePath))
                        {
                            RemoteProvider = new Services.Storage.NetworkStorageProvider(config.NetworkBasePath); // Kept if needed elsewhere, otherwise redundant
                            RemoteSyncEngine = new Services.Sync.SyncEngine(SnapshotStore, NetworkConfigStore, SyncLogger, AuditLogger);
                            
                            // Configurar MasterRootId (en Local Network el root es vacío o la base misma)
                            RemoteSyncEngine.MasterRootId = string.Empty; 
                        }
                }
            }
            catch { /* Not configured or network down */ }
            
            Window = new MainWindow();
            MainWindowInstance = Window;
            
            if (!AuthService.IsAuthenticated)
            {
                NavigateToLogin();
            }
            else
            {
                NavigateToMain();
                
                // Check for pending sync changes after main window is shown
                await CheckAndRunStartupSyncAsync();
            }

            Window.Activate();
        }
        catch (Exception ex)
        {
            // Catch startup errors that crash the app silently
            MessageBox(IntPtr.Zero, $"Startup Error: {ex.Message}\n\n{ex.StackTrace}", "Fatal Startup Error", 0x10);
        }
    }

    public void NavigateToLogin()
    {
        if (Window is MainWindow mw)
        {
            mw.ShowLogin();
        }
    }

    public void NavigateToMain()
    {
        if (Window is MainWindow mw)
        {
            mw.ShowMain();
        }
    }

    private async System.Threading.Tasks.Task CheckAndRunStartupSyncAsync()
    {
        try
        {
            if (RemoteSyncEngine == null) return;

            // Only run if paths are configured
            if (!await NetworkConfigStore.ValidatePathsAsync()) return;

            var config = await NetworkConfigStore.LoadAsync();
            if (!config.AutoSyncOnStartup) return;

            // Execute Sync
            await RemoteSyncEngine.RunSyncAsync();
        }
        catch (Exception ex)
        {
            await SyncLogger.LogErrorAsync("Startup sync failed", ex);
        }
    }
}
