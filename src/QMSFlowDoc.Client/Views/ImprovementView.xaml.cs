using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Client.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;

namespace QMSFlowDoc.Client.Views;

public sealed partial class ImprovementView : Page
{
    private readonly IImprovementService _improvementService;
    
    public ObservableCollection<RiskListDto> Risks { get; } = new();
    public ObservableCollection<AuditListDto> Audits { get; } = new();
    public ObservableCollection<ManagementReviewListDto> Reviews { get; } = new();

    private List<RiskListDto> _allRisks = new();

    public ImprovementView()
    {
        this.InitializeComponent();
        _improvementService = ((App)Application.Current).ImprovementService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadAllData();
    }

    private async Task LoadAllData()
    {
        try
        {
            var risks = await _improvementService.GetRisksAsync();
            _allRisks = risks.ToList();
            ApplyRiskFilter();

            var audits = await _improvementService.GetAuditsAsync();
            Audits.Clear();
            foreach (var a in audits) Audits.Add(a);

            var reviews = await _improvementService.GetReviewsAsync();
            Reviews.Clear();
            foreach (var r in reviews) Reviews.Add(r);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading improvement data: {ex.Message}");
        }
    }

    private void ApplyRiskFilter()
    {
        Risks.Clear();
        foreach (var r in _allRisks)
        {
            if (ActiveRisksFilter.IsOn && r.Status != RiskStatus.ACTIVE) continue;
            Risks.Add(r);
        }
    }

    private void Filter_Toggled(object sender, RoutedEventArgs e)
    {
        ApplyRiskFilter();
    }

    private void AddRisk_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(RiskEditorView));
    }

    // Changed from ItemClick to DoubleTapped for editing
    private void RisksList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        // Get the datum from the source if possible, or use SelectedItem
        if (RisksList.SelectedItem is RiskListDto risk)
        {
             Frame.Navigate(typeof(RiskEditorView), risk.Id);
        }
    }

    // Unused ItemClick (optional: keep for selection feedback only)
    private void RisksList_ItemClick(object sender, ItemClickEventArgs e) { }

    private async void ExportMatrix_Click(object sender, RoutedEventArgs e)
    {
        var savePicker = new Windows.Storage.Pickers.FileSavePicker();
        savePicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("CSV", new List<string>() { ".csv" });
        savePicker.SuggestedFileName = "MatrizRiesgos";
        
        // Initialize the picker with the window handle (WinUI 3 requirement)
        var window = (Application.Current as App)?.MainWindow;
        var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

        var file = await savePicker.PickSaveFileAsync();
        if (file != null)
        {
            var lines = new List<string> { "Titulo,Categoria,Probabilidad,Impacto,Score,Estado" };
            foreach (var r in _allRisks)
            {
                lines.Add($"\"{r.Title}\",\"{r.Category}\",{r.Likelihood},{r.Impact},{r.Score},{r.Status}");
            }
            await Windows.Storage.FileIO.WriteLinesAsync(file, lines);
        }
    }

    private void AuditsList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (AuditsList.SelectedItem is AuditListDto audit)
        {
            Frame.Navigate(typeof(AuditEditorView), audit.Id);
        }
    }

    private void AuditsList_ItemClick(object sender, ItemClickEventArgs e) { }

    private async void AddAudit_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Frame.Navigate(typeof(AuditEditorView));
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Error de Navegación",
                Content = $"No se pudo abrir el editor de auditoría: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private void AddReview_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ReviewEditorView));
    }

    private void ReviewsList_DoubleTapped(object sender, Microsoft.UI.Xaml.Input.DoubleTappedRoutedEventArgs e)
    {
        if (ReviewsList.SelectedItem is ManagementReviewListDto review)
        {
            Frame.Navigate(typeof(ReviewEditorView), review.Id);
        }
    }

    private async void OpenPdf_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid docId)
        {
            if (docId == Guid.Empty)
            {
                 // Should be prevented by binding, but safety check
                 await ShowMessage("Documento no disponible", "El ID del documento es inválido.");
                 return;
            }

            try
            {
                // Usage of DocumentService to get file logic
                var docService = ((App)Application.Current).DocumentService;
                var bytes = await docService.GetFileContentAsync(docId);
                
                if (bytes != null && bytes.Length > 0)
                {
                    // Save to temp and open
                    var tempFolder = Windows.Storage.ApplicationData.Current.TemporaryFolder;
                    var file = await tempFolder.CreateFileAsync($"doc_{docId}.pdf", Windows.Storage.CreationCollisionOption.ReplaceExisting);
                    await Windows.Storage.FileIO.WriteBytesAsync(file, bytes);
                    
                    var options = new Windows.System.LauncherOptions { DisplayApplicationPicker = false };
                    await Windows.System.Launcher.LaunchFileAsync(file, options);
                }
                else
                {
                    await ShowMessage("Documento no encontrado", "No se pudo descargar el contenido del documento. Verifique si existe en el servidor.");
                }
            }
            catch (Exception ex)
            {
                 await ShowMessage("Error al abrir PDF", ex.Message);
            }
        }
        else
        {
             await ShowMessage("Aviso", "Este registro no tiene un documento adjunto.");
        }
    }

    private async void DeleteAudit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid auditId)
        {
             var confirm = await new ContentDialog
            {
                Title = "Confirmar Eliminación",
                Content = "¿Está seguro de que desea eliminar esta auditoría? Esta acción no se puede deshacer.",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            }.ShowAsync();

            if (confirm == ContentDialogResult.Primary)
            {
                try
                {
                    var service = ((App)Application.Current).ImprovementService;
                    var success = await service.DeleteAuditAsync(auditId);
                    if (success)
                    {
                        var audit = Audits.FirstOrDefault(a => a.Id == auditId);
                        if (audit != null) Audits.Remove(audit);
                    }
                    else
                    {
                        await ShowMessage("Error", "No se pudo eliminar la auditoría.");
                    }
                }
                catch (Exception ex)
                {
                    await ShowMessage("Error", ex.Message);
                }
            }
        }
    }

    private async void DeleteReview_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Guid reviewId)
        {
             var confirm = await new ContentDialog
            {
                Title = "Confirmar Eliminación",
                Content = "¿Está seguro de que desea eliminar esta revisión? Esta acción no se puede deshacer.",
                PrimaryButtonText = "Eliminar",
                CloseButtonText = "Cancelar",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            }.ShowAsync();

            if (confirm == ContentDialogResult.Primary)
            {
                try
                {
                    var service = ((App)Application.Current).ImprovementService;
                    var success = await service.DeleteReviewAsync(reviewId);
                    if (success)
                    {
                        var review = Reviews.FirstOrDefault(r => r.Id == reviewId);
                        if (review != null) Reviews.Remove(review);
                    }
                    else
                    {
                        await ShowMessage("Error", "No se pudo eliminar la revisión.");
                    }
                }
                catch (Exception ex)
                {
                    await ShowMessage("Error", ex.Message);
                }
            }
        }
    }

    private async Task ShowMessage(string title, string content)
    {
        await new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OK",
            XamlRoot = this.XamlRoot
        }.ShowAsync();
    }
}
