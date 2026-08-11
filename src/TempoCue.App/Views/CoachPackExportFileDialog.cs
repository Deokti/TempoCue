using System.IO;
using System.Windows;
using Microsoft.Win32;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Одинаково сохраняет переносимый Coach Pack из карточки и экрана подробностей.</summary>
internal static class CoachPackExportFileDialog
{
    public static async Task<bool?> ShowAsync(
        Window? owner,
        CoachingPackDetailsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (!viewModel.CanExportCoachPack)
        {
            return false;
        }

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".json",
            Filter = viewModel.CoachPackExportDialogFilter,
            FileName = FileNameUtilities.Create(
                viewModel.Title,
                ".tempocue.json",
                "TempoCue-coach-pack")
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
            return await viewModel.ExportCoachPackAsync(destination);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            viewModel.ReportCoachPackExportFileFailure();
            return false;
        }
    }
}
