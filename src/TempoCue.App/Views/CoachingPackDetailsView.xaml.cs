using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using Microsoft.Win32;
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

    public event EventHandler? DeleteRequested;

    private CoachingPackDetailsViewModel ViewModel =>
        (CoachingPackDetailsViewModel)DataContext;

    private void BackButton_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void PackEnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        PackEnabledToggle.IsEnabled = false;
        StateChangeRequested?.Invoke(this, EventArgs.Empty);
    }

    public void CompleteStateChange()
    {
        PackEnabledToggle.IsChecked = ViewModel.IsEnabled;
        PackEnabledToggle.IsEnabled = true;
    }

    private void EditActionButton_Click(object sender, RoutedEventArgs e) =>
        EditRequested?.Invoke(this, EventArgs.Empty);

    private void DeleteCoachPackMenuButton_Click(object sender, RoutedEventArgs e)
    {
        PackActionsPopup.IsOpen = false;
        DeleteRequested?.Invoke(this, EventArgs.Empty);
    }

    private void AdditionalActionsButton_Click(object sender, RoutedEventArgs e) =>
        PackActionsPopup.IsOpen = true;

    private void OverviewTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowOverview();

    private void AdviceTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowAdvice();

    private void ItemsTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowItems();

    private async void ExportButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.OpenExportAsync();

    private async void ExportCoachPackMenuButton_Click(object sender, RoutedEventArgs e)
    {
        PackActionsPopup.IsOpen = false;
        if (!ViewModel.CanExportCoachPack)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".json",
            Filter = ViewModel.CoachPackExportDialogFilter,
            FileName = FileNameUtilities.Create(
                ViewModel.Title,
                ".tempocue.json",
                "TempoCue-coach-pack")
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            await using var destination = new FileStream(
                dialog.FileName,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true);
            await ViewModel.ExportCoachPackAsync(destination);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            ViewModel.ReportCoachPackExportFileFailure();
        }
    }

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
