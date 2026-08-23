using System.IO;
using System.Windows;
using Microsoft.Win32;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>
/// Сохраняет запись последовательности кадров только после явного выбора файла.
/// Как и одиночный кадр, фоновый захват сам ничего на диск не пишет.
/// </summary>
internal static class MinimapReplaySequenceFileDialog
{
    public static async Task<bool?> ShowAsync(
        Window? owner,
        CaptureDiagnosticsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (!viewModel.HasRecordedSequence)
        {
            return false;
        }

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".tempocue-sequence",
            Filter = viewModel.SequenceDialogFilter,
            FileName = $"TempoCue-sequence-{DateTime.Now:yyyyMMdd-HHmmss}.tempocue-sequence"
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
            return await viewModel.SaveRecordedSequenceAsync(destination);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            viewModel.ReportReplaySampleExportFailure();
            return false;
        }
    }
}
