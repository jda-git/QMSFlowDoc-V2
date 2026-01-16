using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using QMSFlowDoc.Client.Services;
using QMSFlowDoc.Shared.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class EQAConfigurationView : Page
{
    private readonly LocalDocumentStore _store;

    public EQAConfigurationView()
    {
        this.InitializeComponent();
        _store = (Application.Current as App)!.LocalStore;
        this.Loaded += EQAConfigurationView_Loaded;
    }

    private async void EQAConfigurationView_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadProvidersAsync();
        await LoadSchemesAsync();
    }

    private async Task LoadProvidersAsync()
    {
        var providers = await _store.GetEQAProvidersAsync();
        ProvidersList.ItemsSource = providers;
        
        // Update filter combo
        var filterList = new List<EQAProviderDto> { new EQAProviderDto(Guid.Empty, "Todos", null, null, true) };
        filterList.AddRange(providers);
        ProviderFilterCombo.ItemsSource = filterList;
        if (ProviderFilterCombo.SelectedItem == null) ProviderFilterCombo.SelectedIndex = 0;
    }

    private async Task LoadSchemesAsync()
    {
        Guid? filterId = null;
        if (ProviderFilterCombo.SelectedItem is EQAProviderDto selected && selected.Id != Guid.Empty)
        {
            filterId = selected.Id;
        }

        var schemes = await _store.GetEQASchemesAsync(filterId);
        SchemesList.ItemsSource = schemes;
    }

    private void RefreshProviders_Click(object sender, RoutedEventArgs e) => _ = LoadProvidersAsync();
    private void RefreshSchemes_Click(object sender, RoutedEventArgs e) => _ = LoadSchemesAsync();

    private void ProviderFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _ = LoadSchemesAsync();
    }

    private async void AddProvider_Click(object sender, RoutedEventArgs e)
    {
        await ShowProviderDialog();
    }

    private async void EditProvider_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is EQAProviderDto provider)
        {
            await ShowProviderDialog(provider);
        }
    }

    private async void AddScheme_Click(object sender, RoutedEventArgs e)
    {
        await ShowSchemeDialog();
    }

    private async void EditScheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is EQASchemeDto scheme)
        {
            await ShowSchemeDialog(scheme);
        }
    }

    private async Task ShowProviderDialog(EQAProviderDto? existing = null)
    {
        var nameBox = new TextBox { Header = "Nombre", Text = existing?.Name ?? "" };
        var codeBox = new TextBox { Header = "Código", Text = existing?.Code ?? "" };
        var contactBox = new TextBox { Header = "Contacto", Text = existing?.ContactInfo ?? "" };
        var activeSwitch = new ToggleSwitch { Header = "Estado", OnContent = "Activo", OffContent = "Inactivo", IsOn = existing?.IsActive ?? true };

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(nameBox);
        stack.Children.Add(codeBox);
        stack.Children.Add(contactBox);
        stack.Children.Add(activeSwitch);

        var dialog = new ContentDialog
        {
            Title = existing == null ? "Nuevo Proveedor" : "Editar Proveedor",
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            Content = stack,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text)) return;

            var provider = new EQAProviderDto(
                existing?.Id ?? Guid.NewGuid(),
                nameBox.Text,
                string.IsNullOrWhiteSpace(codeBox.Text) ? null : codeBox.Text,
                string.IsNullOrWhiteSpace(contactBox.Text) ? null : contactBox.Text,
                activeSwitch.IsOn
            );

            await _store.UpsertEQAProviderAsync(provider);
            await LoadProvidersAsync();
        }
    }

    private async Task ShowSchemeDialog(EQASchemeDto? existing = null)
    {
        var providers = await _store.GetEQAProvidersAsync();
        if (!providers.Any())
        {
            var noProvDialog = new ContentDialog
            {
                Title = "Error",
                Content = "Debe crear al menos un proveedor antes de crear esquemas.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await noProvDialog.ShowAsync();
            return;
        }

        var providerCombo = new ComboBox { Header = "Proveedor", ItemsSource = providers, DisplayMemberPath = "Name", HorizontalAlignment = HorizontalAlignment.Stretch };
        if (existing != null)
        {
            providerCombo.SelectedItem = providers.FirstOrDefault(p => p.Id == existing.ProviderId);
        }
        else if (ProviderFilterCombo.SelectedItem is EQAProviderDto filter && filter.Id != Guid.Empty)
        {
            providerCombo.SelectedItem = providers.FirstOrDefault(p => p.Id == filter.Id);
        }

        var nameBox = new TextBox { Header = "Nombre del Esquema", Text = existing?.Name ?? "" };
        var matrixBox = new TextBox { Header = "Matriz (ej. Sangre, Suero)", Text = existing?.Matrix ?? "" };
        var periodBox = new TextBox { Header = "Periodicidad", Text = existing?.Periodicity ?? "" };
        var notesBox = new TextBox { Header = "Notas", Text = existing?.Notes ?? "", AcceptsReturn = true, Height = 80 };

        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(providerCombo);
        stack.Children.Add(nameBox);
        stack.Children.Add(matrixBox);
        stack.Children.Add(periodBox);
        stack.Children.Add(notesBox);

        var dialog = new ContentDialog
        {
            Title = existing == null ? "Nuevo Esquema" : "Editar Esquema",
            PrimaryButtonText = "Guardar",
            CloseButtonText = "Cancelar",
            DefaultButton = ContentDialogButton.Primary,
            Content = stack,
            XamlRoot = this.XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text) || providerCombo.SelectedItem == null) return;
            
            var selectedProvider = (EQAProviderDto)providerCombo.SelectedItem;

            var scheme = new EQASchemeDto(
                existing?.Id ?? Guid.NewGuid(),
                selectedProvider.Id,
                selectedProvider.Name, // This is just for UI, might be redundant in DTO if we join, but keeping consistent
                nameBox.Text,
                string.IsNullOrWhiteSpace(matrixBox.Text) ? null : matrixBox.Text,
                string.IsNullOrWhiteSpace(periodBox.Text) ? null : periodBox.Text,
                existing?.ResponsibleUserId, // Not handling user selection yet in this dialog
                string.IsNullOrWhiteSpace(notesBox.Text) ? null : notesBox.Text
            );

            await _store.UpsertEQASchemeAsync(scheme);
            await LoadSchemesAsync();
        }
    }
}
