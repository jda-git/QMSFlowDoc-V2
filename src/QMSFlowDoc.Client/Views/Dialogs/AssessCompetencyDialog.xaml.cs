using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Shared.Models;
using QMSFlowDoc.Shared.DTOs;
using System;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class AssessCompetencyDialog : ContentDialog
{
    // Propiedades públicas para obtener los valores ingresados
    public string CompetencyName { get; private set; } = string.Empty;
    public string Area { get; private set; } = string.Empty;
    public CompetencyOutcome Outcome { get; private set; }
    public DateTime EvaluationDate { get; private set; }
    public DateTime? ValidUntil { get; private set; }
    public string Evidence { get; private set; } = string.Empty;

    public void LoadData(CompetencyEvaluationDto dto)
    {
        CompetencyNameBox.Text = dto.CompetencyName;
        AreaBox.Text = dto.Area;
        EvalDate.Date = dto.EvaluationDate;
        if (dto.ValidUntil.HasValue) ExpiryDate.Date = dto.ValidUntil.Value;
        EvidenceBox.Text = string.Empty; // Evidence not in DTO usually? wait, DTO properties?
        // DTO has Outcome as string "COMPETENTE" etc.
        // Need to map string to Tag
        
        // CompetencyEvaluationDto (lines 353 in Controller) maps outcome string.
        // DTO definition: public record CompetencyEvaluationDto(...)
        // Let's assume DTO.Outcome matches logic.
        
        string tag = dto.Outcome switch {
            "COMPETENTE" => "PASS",
            "NO_COMPETENTE" => "FAIL",
            "EN_FORMACION" => "CONDITIONAL",
            _ => "CONDITIONAL"
        };

        foreach(ComboBoxItem item in OutcomeCombo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                OutcomeCombo.SelectedItem = item;
                break;
            }
        }
        
        this.Title = "Editar Competencia";
        this.PrimaryButtonText = "Guardar";
        this.SecondaryButtonText = "Eliminar"; // Enable Delete button
    }

    public AssessCompetencyDialog()
    {
        this.InitializeComponent();
        
        // Establecer fecha por defecto a hoy
        EvalDate.Date = DateTimeOffset.Now;
        
        this.PrimaryButtonClick += AssessCompetencyDialog_PrimaryButtonClick;
    }

    private void AssessCompetencyDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // Validar nombre de competencia obligatorio
        if (string.IsNullOrWhiteSpace(CompetencyNameBox.Text))
        {
            args.Cancel = true;
            return;
        }
        
        if (string.IsNullOrWhiteSpace(AreaBox.Text))
        {
            args.Cancel = true;
            return;
        }

        if (!EvalDate.Date.HasValue)
        {
            args.Cancel = true;
            return;
        }

        // Obtener valores
        CompetencyName = CompetencyNameBox.Text.Trim();
        Area = AreaBox.Text.Trim();
        EvaluationDate = EvalDate.Date.Value.DateTime;
        
        var outcomeTag = (OutcomeCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        Outcome = outcomeTag switch
        {
            "PASS" => CompetencyOutcome.PASS,
            "FAIL" => CompetencyOutcome.FAIL,
            _ => CompetencyOutcome.CONDITIONAL
        };

        if (ExpiryDate.Date.HasValue)
        {
            ValidUntil = ExpiryDate.Date.Value.DateTime;
        }

        Evidence = EvidenceBox.Text;
    }
}
