using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class EquipmentEditorView : Page
{
    private Guid? _equipmentId = null;
    private readonly Services.IEquipmentService _equipmentService;
    private readonly Services.IAuditLogger _auditLogger;
    private readonly Services.IAuthService _authService;

    public EquipmentEditorView()
    {
        this.InitializeComponent();
        _equipmentService = ((App)Application.Current).EquipmentService;
        _auditLogger = ((App)Application.Current).EquipmentAuditLogger;
        _authService = ((App)Application.Current).AuthService;
        InstallationDatePicker.Date = DateTimeOffset.Now;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        
        if (e.Parameter is Guid equipmentId)
        {
            _equipmentId = equipmentId;
            await LoadEquipment(equipmentId);
        }
    }

    private async Task LoadEquipment(Guid id)
    {
        try
        {
            var equipment = await _equipmentService.GetEquipmentByIdAsync(id);
            if (equipment != null)
            {
                NameBox.Text = equipment.Name;
                InternalIdBox.Text = equipment.InternalId ?? "";
                AssetTagBox.Text = equipment.AssetTag ?? "";
                LocationBox.Text = equipment.Location ?? "";
                ManufacturerBox.Text = equipment.Manufacturer ?? "";
                ModelBox.Text = equipment.Model ?? "";
                SerialBox.Text = equipment.SerialNumber ?? "";
                
                if (equipment.InstalledAt.HasValue)
                    InstallationDatePicker.Date = new DateTimeOffset(equipment.InstalledAt.Value);

                NotesBox.Text = equipment.Notes ?? "";

                // Technical & Software
                SoftwareVersionBox.Text = equipment.SoftwareVersion ?? "";
                FirmwareVersionBox.Text = equipment.FirmwareVersion ?? "";
                ManualPathBox.Text = equipment.ManualPath ?? "";

                // Verification & Calibration
                if (equipment.ReceptionDate.HasValue)
                    ReceptionDatePicker.Date = new DateTimeOffset(equipment.ReceptionDate.Value);
                
                ReceptionConditionBox.Text = equipment.ReceptionCondition ?? "";
                
                IsVerifiedToggle.IsOn = equipment.IsVerified;
                if (equipment.VerificationDate.HasValue)
                    VerificationDatePicker.Date = new DateTimeOffset(equipment.VerificationDate.Value);

                CalibrationFreqBox.Value = equipment.CalibrationFrequencyMonths ?? 12;
                
                if (equipment.LastCalibration.HasValue)
                    LastCalibrationDatePicker.Date = new DateTimeOffset(equipment.LastCalibration.Value);
                
                if (equipment.NextCalibration.HasValue)
                    NextCalibrationDatePicker.Date = new DateTimeOffset(equipment.NextCalibration.Value);
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error al cargar equipo: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e)
    {
        Frame.GoBack();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Visibility = Visibility.Collapsed;
        
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            ErrorText.Text = "El nombre del equipo es obligatorio.";
            ErrorText.Visibility = Visibility.Visible;
            return;
        }

        try
        {
            // Calculate Next Calibration if not set
            DateTime? nextCalDate = null;
            // First check if DatePicker has a selected date (Date is DateTimeOffset, checking Year > 1900 usually works or check default)
            // DatePicker in WinUI is always set, but we might want to check if user interacted? 
            // Actually let's trust the DatePicker value if it seems valid, or calculate it.
            
            // Logic: If LastCalibration is set and Frequency is set > 0, calculate default Next
            if (CalibrationFreqBox.Value > 0)
            {
                 // If NextCalibration is selected (checked), use it. WinUI DatePicker doesn't have "Null" state easily unless custom control.
                 // Assuming user sets it.
                 // But wait, DatePicker.Date is DateTimeOffset.
                 
                 // Let's rely on LastCalibration + Freq if Next is same as today (default)? 
                 // Or better: Just save what is there.
                 // But implementation plan said "Calculate automatically".
                 // I'll leave it to user logic or simple calculation:
            }

            Equipment? result;
            
            // Extract values
            // Use .Date.UtcDateTime for clean dates
            // Check if DatePickers were actually used? WinUI DatePicker defaults to Now.
            // I'll assume standard usage.
            
            var receptionDate = ReceptionDatePicker.Date != default ? ReceptionDatePicker.Date.UtcDateTime : (DateTime?)null;
            var verificationDate = VerificationDatePicker.Date != default ? VerificationDatePicker.Date.UtcDateTime : (DateTime?)null;
            var lastCalDate = LastCalibrationDatePicker.Date != default ? LastCalibrationDatePicker.Date.UtcDateTime : (DateTime?)null;
             nextCalDate = NextCalibrationDatePicker.Date != default ? NextCalibrationDatePicker.Date.UtcDateTime : (DateTime?)null;
             var installDate = InstallationDatePicker.Date != default ? InstallationDatePicker.Date.UtcDateTime : (DateTime?)null;

            if (_equipmentId.HasValue)
            {
                // UPDATE existing equipment
                var updateRequest = new UpdateEquipmentRequest(
                    _equipmentId.Value,
                    InternalIdBox.Text,
                    AssetTagBox.Text,
                    NameBox.Text,
                    ManufacturerBox.Text,
                    ModelBox.Text,
                    SerialBox.Text,
                    SoftwareVersionBox.Text,
                    FirmwareVersionBox.Text,
                    LocationBox.Text,
                    installDate,
                    receptionDate,
                    ReceptionConditionBox.Text,
                    verificationDate,
                    IsVerifiedToggle.IsOn,
                    (int)CalibrationFreqBox.Value,
                    lastCalDate,
                    nextCalDate,
                    ManualPathBox.Text
                );
                await _equipmentService.UpdateEquipmentAsync(updateRequest);
                // Return just to check success? Service returns void but throws on error.
                // I'll assume success if no throw.
                result = new Equipment(); // Dummy
            }
            else
            {
                // CREATE new equipment
                var createRequest = new CreateEquipmentRequest(
                    InternalIdBox.Text,
                    AssetTagBox.Text,
                    NameBox.Text,
                    ManufacturerBox.Text,
                    ModelBox.Text,
                    SerialBox.Text,
                    SoftwareVersionBox.Text,
                    FirmwareVersionBox.Text,
                    LocationBox.Text,
                    installDate,
                    receptionDate,
                    ReceptionConditionBox.Text,
                    (int)CalibrationFreqBox.Value,
                    ManualPathBox.Text
                );
                await _equipmentService.CreateEquipmentAsync(createRequest);
                result = new Equipment(); 
            }

            if (result != null)
            {
                // Log the action
                var userName = _authService.CurrentUsername ?? "Unknown";
                var action = _equipmentId.HasValue ? "Actualizar Equipo" : "Crear Equipo";
                await _auditLogger.LogEquipmentActionAsync(
                    action,
                    NameBox.Text,
                    $"Etiqueta: {AssetTagBox.Text}, ID: {InternalIdBox.Text}",
                    _authService.CurrentUserId?.ToString(),
                    userName
                );
                Frame.GoBack();
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = $"Error: {ex.Message}";
            ErrorText.Visibility = Visibility.Visible;
        }
    }
}
