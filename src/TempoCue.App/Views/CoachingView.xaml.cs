using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Библиотека матчапов, правил карты и пользовательских Coach Packs.</summary>
public partial class CoachingView : UserControl
{
    public CoachingView()
    {
        InitializeComponent();
    }

    private CoachingViewModel ViewModel =>
        (CoachingViewModel)DataContext;

    // Переходы вложенного сценария централизованы здесь, чтобы дочерние View не знали друг о друге.
    private void CoachingLibraryPage_CreateRequested(object? sender, EventArgs e) =>
        ViewModel.OpenPackTypeSelection();

    private async void CoachingLibraryPage_PackOpenRequested(
        object? sender,
        CoachingPackEventArgs e) =>
        await ViewModel.OpenPackDetailsAsync(e.Card);

    private void CoachingPackDetailsPage_BackRequested(object? sender, EventArgs e)
    {
        ViewModel.Details.Export.Close();
        ViewModel.OpenLibrary();
    }

    private async void CoachingPackDetailsPage_StateChangeRequested(object? sender, EventArgs e)
    {
        try
        {
            await ViewModel.SetSelectedPackEnabledAsync(!ViewModel.Details.IsEnabled);
        }
        finally
        {
            CoachingPackDetailsPage.CompleteStateChange();
        }
    }

    private async void CoachingPackDetailsPage_EditRequested(object? sender, EventArgs e) =>
        await ViewModel.OpenSelectedPackEditorAsync();

    private void CoachingPackDetailsPage_DeleteRequested(object? sender, EventArgs e)
    {
        if (!ViewModel.Details.CanDelete)
        {
            return;
        }

        ViewModel.OpenDeleteConfirmation();
        Dispatcher.BeginInvoke(
            () => Keyboard.Focus(CancelDeleteCoachPackButton),
            DispatcherPriority.Input);
    }

    private void CancelDeleteCoachPackButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.CloseDeleteConfirmation();

    private void DeleteConfirmationOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        ViewModel.CloseDeleteConfirmation();
        e.Handled = true;
    }

    private async void ConfirmDeleteCoachPackButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CloseDeleteConfirmation();
        ConfirmDeleteCoachPackButton.IsEnabled = false;
        CancelDeleteCoachPackButton.IsEnabled = false;
        var deleted = false;
        try
        {
            deleted = await ViewModel.DeleteSelectedPackAsync();
        }
        finally
        {
            ConfirmDeleteCoachPackButton.IsEnabled = true;
            CancelDeleteCoachPackButton.IsEnabled = true;
        }

        if (!deleted)
        {
            MessageBox.Show(
                Window.GetWindow(this),
                ViewModel.Details.DeleteFailureMessage,
                ViewModel.Details.DeleteFailureTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void PackTypeSelectionPage_BackRequested(object? sender, EventArgs e) =>
        ViewModel.OpenLibrary();

    private void PackTypeSelectionPage_MatchupSelected(object? sender, EventArgs e) =>
        ViewModel.OpenMatchupEditor();

    private void MatchupEditorPage_BackRequested(object? sender, EventArgs e) =>
        ViewModel.CloseMatchupEditor();

    private async void MatchupEditorPage_SaveCompleted(object? sender, EventArgs e)
    {
        ViewModel.OpenLibrary();
        await ViewModel.Library.RefreshAsync();
    }
}
