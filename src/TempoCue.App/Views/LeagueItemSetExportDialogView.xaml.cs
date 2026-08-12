using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Модальное подтверждение блоков и сохранение League item set JSON.</summary>
public partial class LeagueItemSetExportDialogView : UserControl
{
    public LeagueItemSetExportDialogView()
    {
        InitializeComponent();
    }

    private LeagueItemSetExportDialogViewModel ViewModel =>
        (LeagueItemSetExportDialogViewModel)DataContext;

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Close();

    private void LeagueItemSetExportDialogView_PreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel.Close();
            e.Handled = true;
        }
    }

    private async void ExportOption_Changed(object sender, RoutedEventArgs e) =>
        await ViewModel.RefreshAsync();

    private async void ExportTitle_LostFocus(object sender, RoutedEventArgs e) =>
        await ViewModel.RefreshAsync();

    private async void CopyJsonButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
        if (!ViewModel.CanExport)
        {
            return;
        }

        try
        {
            Clipboard.SetText(ViewModel.Json);
            ViewModel.MarkCopied();
        }
        catch (ExternalException)
        {
            ViewModel.MarkClipboardFailed();
        }
    }

    private async void DownloadJsonButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.RefreshAsync();
        if (!ViewModel.CanExport)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".json",
            Filter = "JSON (*.json)|*.json",
            FileName = FileNameUtilities.Create(
                ViewModel.Title,
                ".json",
                "TempoCue-item-set")
        };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, ViewModel.Json);
            ViewModel.MarkDownloaded();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            ViewModel.MarkFileWriteFailed();
        }
    }

}
