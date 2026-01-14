using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.Models;
using System;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class AddFindingDialog : ContentDialog
{
    public FindingType FindingType { get; private set; }
    public string IsoRequirement { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;

    public AddFindingDialog()
    {
        this.InitializeComponent();
        this.PrimaryButtonClick += AddFindingDialog_PrimaryButtonClick;
    }

    private void AddFindingDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
        {
            args.Cancel = true;
            DescriptionBox.Header = "Descripción (Requerido)";
            return;
        }

        Description = DescriptionBox.Text;
        IsoRequirement = IsoRequirementBox.Text;
        
        var typeTag = (TypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        FindingType = typeTag switch
        {
            "MINOR_NC" => FindingType.MINOR_NC,
            "MAJOR_NC" => FindingType.MAJOR_NC,
            _ => FindingType.OBSERVATION
        };
    }
}
