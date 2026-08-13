using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Показывает сохранённый набор контроля карты и его действия.</summary>
public partial class MapControlPackDetailsView : UserControl
{
    public MapControlPackDetailsView() => InitializeComponent();

    public event EventHandler? BackRequested;
    public event EventHandler? StateChangeRequested;
    public event EventHandler? EditRequested;
    public event EventHandler? DeleteRequested;

    private MapControlPackDetailsViewModel ViewModel =>
        (MapControlPackDetailsViewModel)DataContext;

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void PackEnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        MapControlPackEnabledToggle.IsEnabled = false;
        StateChangeRequested?.Invoke(this, EventArgs.Empty);
    }

    public void CompleteStateChange()
    {
        MapControlPackEnabledToggle.IsChecked = ViewModel.IsEnabled;
        MapControlPackEnabledToggle.IsEnabled = true;
    }

    private void EditActionButton_Click(object sender, RoutedEventArgs e) =>
        EditRequested?.Invoke(this, EventArgs.Empty);

    private async void PackActionsMenu_ExportRequested(object? sender, EventArgs e) =>
        await MapControlPackExportFileDialog.ShowAsync(Window.GetWindow(this), ViewModel);

    private void PackActionsMenu_DeleteRequested(object? sender, EventArgs e) =>
        DeleteRequested?.Invoke(this, EventArgs.Empty);

    private void OverviewTabButton_Click(object sender, RoutedEventArgs e) => ViewModel.ShowOverview();
    private void ObjectivesTabButton_Click(object sender, RoutedEventArgs e) => ViewModel.ShowObjectives();
    private void TimedTabButton_Click(object sender, RoutedEventArgs e) => ViewModel.ShowTimedReminders();
    private void PersistentTabButton_Click(object sender, RoutedEventArgs e) => ViewModel.ShowPersistentTips();

    private void SourceLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException)
        {
            ViewModel.ReportSourceLinkOpenFailure();
            e.Handled = true;
        }
    }
}
