using Microsoft.UI.Xaml.Controls;
using System;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class AddDailyQCDialog : ContentDialog
{
    public bool IsPass { get; private set; }
    public string LotNumber { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTime PerformedAt { get; private set; }

    public AddDailyQCDialog()
    {
        this.InitializeComponent();
        DateBox.Date = DateTimeOffset.Now;
        this.PrimaryButtonClick += AddDailyQCDialog_PrimaryButtonClick;
    }

    private void AddDailyQCDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validation
        if (string.IsNullOrWhiteSpace(LotNumberBox.Text))
        {
            // Requirement: Lot Traceability
            // Force lot number? Let's say yes for compliance.
            // Actually, maybe show error.
            // Since we can't easily show error inline without extra UI, let's just mark the box header red or something?
            // Or simpler: block close and set error text if we had one.
            // For now, let's just require it.
            // But wait, user might not have it handy.
            // Let's make it mandatory as per ISO 6.3 requirement.
            
            // Just block closing (args.Cancel = true) but we need to tell user why.
            // I'll assume simple validation for now:
            if (string.IsNullOrWhiteSpace(LotNumberBox.Text))
            {
               args.Cancel = true;
               LotNumberBox.Header = "Lote de Beads/Reactivo (Requerido)";
               // Ideally set foreground to red, but string is simple.
               return; 
            }
        }
        
        LotNumber = LotNumberBox.Text;
        Notes = NotesBox.Text;
        PerformedAt = DateBox.Date?.DateTime ?? DateTime.Now;
        
        var selected = ResultCombo.SelectedItem as ComboBoxItem;
        IsPass = selected?.Tag?.ToString() == "PASS";
    }
}
