using System.IO;
using System.Windows;
using Microsoft.Win32;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Сохраняет переносимый набор контроля карты из карточки или подробностей.</summary>
internal static class MapControlPackExportFileDialog
{
    public static async Task<bool?> ShowAsync(Window? owner, MapControlPackDetailsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        if (!viewModel.CanExport)
        {
            return false;
        }

        var dialog = new SaveFileDialog
        {
            AddExtension = true,
            DefaultExt = ".json",
            Filter = viewModel.ExportDialogFilter,
            FileName = FileNameUtilities.Create(
                viewModel.Title,
                ".tempocue.json",
                "TempoCue-map-control-pack")
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
                4096,
                useAsync: true);
            return await viewModel.ExportAsync(destination);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            viewModel.ReportExportFileFailure();
            return false;
        }
    }
}
