using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using QMSFlowDoc.Shared.DTOs;
using QMSFlowDoc.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace QMSFlowDoc.Client.Views;

public sealed partial class ComplaintsView : Page
{
    public ObservableCollection<ComplaintListDto> Complaints { get; } = new();
    private List<ComplaintListDto> _allComplaints = new();

    public ComplaintsView()
    {
        this.InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await LoadComplaints();
    }

    private async Task LoadComplaints()
    {
        try
        {
            var store = ((App)Application.Current).LocalStore;
            _allComplaints = await store.GetComplaintsAsync();
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading complaints: {ex.Message}");
        }
    }

    private void ApplyFilter()
    {
        Complaints.Clear();
        var filter = (StatusFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();

        foreach (var c in _allComplaints)
        {
            if (filter == "Abiertas" && c.Status != ComplaintStatus.OPEN) continue;
            if (filter == "En Investigación" && c.Status != ComplaintStatus.INVESTIGATING) continue;
            if (filter == "Cerradas" && c.Status != ComplaintStatus.CLOSED) continue;
            Complaints.Add(c);
        }
    }

    private void StatusFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_allComplaints.Count > 0 || StatusFilter.SelectedItem != null) ApplyFilter();
    }

    private void AddComplaint_Click(object sender, RoutedEventArgs e)
    {
        Frame.Navigate(typeof(ComplaintEditorView));
    }

    private void ComplaintsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ComplaintListDto complaint)
        {
            Frame.Navigate(typeof(ComplaintEditorView), complaint.Id);
        }
    }
}
