using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Read-only просмотр сохранённого Coach Pack и вход в экспорт предметов.</summary>
public partial class CoachingPackDetailsView : UserControl
{
    public CoachingPackDetailsView()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

    public event EventHandler? StateChangeRequested;

    public event EventHandler? EditRequested;

    private CoachingPackDetailsViewModel ViewModel =>
        (CoachingPackDetailsViewModel)DataContext;

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void StateActionButton_Click(object sender, RoutedEventArgs e) =>
        StateChangeRequested?.Invoke(this, EventArgs.Empty);

    private void EditActionButton_Click(object sender, RoutedEventArgs e) =>
        EditRequested?.Invoke(this, EventArgs.Empty);

    private void OverviewTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowOverview();

    private void AdviceTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowAdvice();

    private void ItemsTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowItems();

    private async void ExportButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.OpenExportAsync();

    private void SourceLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (!string.Equals(e.Uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(e.Uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }
        catch (Exception exception) when (
            exception is Win32Exception or InvalidOperationException)
        {
            ViewModel.ReportSourceLinkOpenFailure();
            e.Handled = true;
        }
    }
}
