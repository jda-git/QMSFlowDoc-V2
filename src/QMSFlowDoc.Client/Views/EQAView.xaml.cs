using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Linq;
using System.Threading.Tasks;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;

namespace QMSFlowDoc.Client.Views;

public sealed partial class EQAView : Page
{
    private readonly IEQAService _eqaService;
    private EQASchemeDto? _selectedScheme;

    public EQAView()
    {
        this.InitializeComponent();
        _eqaService = ((App)Application.Current).EQAService;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadSchemesAsync();
    }

    private async Task LoadSchemesAsync()
    {
        try
        {
            var schemes = await _eqaService.GetSchemesAsync();
            SchemeList.ItemsSource = schemes;

            if (_selectedScheme != null)
            {
                var reselect = schemes.FirstOrDefault(s => s.Id == _selectedScheme.Id);
                if (reselect != null)
                {
                    SchemeList.SelectedItem = reselect;
                }
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Error cargando esquemas", ex.Message);
        }
    }

    private void Configuration_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(EQAConfigurationView));
    }

    private async void SchemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SchemeList.SelectedItem is EQASchemeDto scheme)
        {
            _selectedScheme = scheme;
            DetailPane.Visibility = Visibility.Visible;
            EmptySelectionPane.Visibility = Visibility.Collapsed;
            
            // Bind Header
            DetailTitle.Text = scheme.Name;
            DetailProvider.Text = $"{scheme.ProviderName} • {scheme.Periodicity ?? "Sin frecuencia"}";

            await LoadRoundsAsync(scheme.Id);
        }
        else
        {
            _selectedScheme = null;
            DetailPane.Visibility = Visibility.Collapsed;
            EmptySelectionPane.Visibility = Visibility.Visible;
        }
    }

    private async Task LoadRoundsAsync(Guid schemeId)
    {
        try
        {
            var rounds = await _eqaService.GetRoundsAsync(schemeId);
            RoundsList.ItemsSource = rounds;
            EmptyRoundsText.Visibility = rounds.Any() ? Visibility.Collapsed : Visibility.Visible;
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Error cargando rondas", ex.Message);
        }
    }

    private async void AddRound_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedScheme == null) return;

        var codeBox = new TextBox { Header = "Código de Ronda / Ciclo", PlaceholderText = "Ej. 2024-C1" };
        var datePicker = new DatePicker { Header = "Fecha de Recepción", Date = DateTime.Now };
        var notesBox = new TextBox { Header = "Notas", AcceptsReturn = true, Height = 60 };

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(codeBox);
        stack.Children.Add(datePicker);
        stack.Children.Add(notesBox);

        var dialog = new ContentDialog
        {
            Title = "Registrar Nueva Ronda",
            PrimaryButtonText = "Crear",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            Content = stack,
            XamlRoot = this.Content.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(codeBox.Text)) return;

                var newRound = new EQARoundDto(
                    Guid.NewGuid(),
                    _selectedScheme.Id,
                    _selectedScheme.Name,
                    codeBox.Text,
                    DateTime.Now.Year,
                    datePicker.Date.DateTime,
                    null, null, null, null, null,
                    "OPEN",
                    null,
                    string.IsNullOrWhiteSpace(notesBox.Text) ? null : notesBox.Text,
                    null, null, null
                );

                await _eqaService.UpsertRoundAsync(newRound);
                await LoadRoundsAsync(_selectedScheme.Id);
            }
            catch (Exception ex)
            {
                await ShowErrorAsync("Error al registrar ronda", ex.Message);
            }
        }
    }

    private async void ViewRoundResults_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is Button btn && btn.Tag is EQARoundDto round)
            {
                Frame.Navigate(typeof(EQARoundDetailsView), round.Id);
            }
            else
            {
                await ShowErrorAsync("Debug Info", $"Button Tag is null or not EQARoundDto. Sender: {sender?.GetType().Name}");
            }
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Error de Navegación", $"No se pudo abrir los detalles de la ronda: {ex.Message}");
        }
    }

    private async Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    public static string FormatDate(DateTime? d) => d?.ToString("d") ?? "-";
}
