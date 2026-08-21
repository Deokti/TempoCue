using System.IO;
using System.Windows;
using Microsoft.Win32;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Сохраняет один диагностический кадр только после явного выбора файла.</summary>
internal static class MinimapReplaySampleFileDialog
{
    public static async Task<bool?> ShowAsync(
        Window? owner,
        CaptureDiagnosticsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (!viewModel.CanSaveReplaySample)
        {
            return false;
        }

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".tempocue-vision",
            Filter = viewModel.ReplaySampleDialogFilter,
            FileName = $"TempoCue-minimap-{DateTime.Now:yyyyMMdd-HHmmss}.tempocue-vision"
        };
        if (dialog.ShowDialog(owner) != true)
        {
            return null;
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
            return await viewModel.SaveReplaySampleAsync(destination);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            viewModel.ReportReplaySampleExportFailure();
            return false;
        }
    }
}
