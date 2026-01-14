using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class AddCapaDialog : ContentDialog
{
    public CAPAActionType ActionType { get; private set; }
    public string ActionDescription { get; private set; } = string.Empty;
    public DateTime? DueDate { get; private set; }
    public Guid? OwnerUserId { get; private set; }

    public AddCapaDialog(List<User> users)
    {
        this.InitializeComponent();
        OwnerCombo.ItemsSource = users;
        if (users.Any()) OwnerCombo.SelectedIndex = 0;
        
        DueDatePicker.Date = DateTimeOffset.Now.AddDays(7); // Default 1 week
        this.PrimaryButtonClick += AddCapaDialog_PrimaryButtonClick;
    }

    private void AddCapaDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (string.IsNullOrWhiteSpace(DescriptionBox.Text))
        {
            args.Cancel = true;
            DescriptionBox.Header = "Descripción (Requerido)";
            return;
        }

        ActionDescription = DescriptionBox.Text;
        DueDate = DueDatePicker.Date.DateTime;
        
        var typeTag = (TypeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        ActionType = typeTag == "PREVENTIVE" ? CAPAActionType.PREVENTIVE : CAPAActionType.CORRECTIVE;

        if (OwnerCombo.SelectedItem is User user)
        {
            OwnerUserId = user.Id;
        }
    }
}
