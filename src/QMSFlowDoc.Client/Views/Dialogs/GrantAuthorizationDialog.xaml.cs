using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.DTOs;
using System;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class GrantAuthorizationDialog : ContentDialog
{
    // Propiedades públicas para obtener los valores ingresados
    public string TaskName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public DateTime ValidFrom { get; private set; }
    public DateTime? ValidUntil { get; private set; }

    public void LoadData(StaffAuthorizationDto dto)
    {
        TaskNameBox.Text = dto.AuthorizationName;
        DescriptionBox.Text = dto.Description;
        FromDate.Date = dto.ValidFrom;
        if (dto.ValidUntil.HasValue) UntilDate.Date = dto.ValidUntil.Value;
        
        this.Title = "Editar Autorización";
        this.PrimaryButtonText = "Guardar";
        this.SecondaryButtonText = "Eliminar"; // Enable Delete button
    }

    public GrantAuthorizationDialog()
    {
        this.InitializeComponent();
        
        // Establecer fecha por defecto a hoy
        FromDate.Date = DateTimeOffset.Now;
        
        this.PrimaryButtonClick += GrantAuthorizationDialog_PrimaryButtonClick;
    }

    private void GrantAuthorizationDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validar nombre de tarea obligatorio
        if (string.IsNullOrWhiteSpace(TaskNameBox.Text))
        {
            args.Cancel = true;
            return;
        }
        
        if (!FromDate.Date.HasValue)
        {
            args.Cancel = true;
            return;
        }

        // Obtener valores
        TaskName = TaskNameBox.Text.Trim();
        Description = string.IsNullOrWhiteSpace(DescriptionBox.Text) ? null : DescriptionBox.Text.Trim();
        ValidFrom = FromDate.Date.Value.DateTime;
        
        if (UntilDate.Date.HasValue)
        {
            ValidUntil = UntilDate.Date.Value.DateTime;
        }
    }
}
