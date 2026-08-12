using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Карточная библиотека встроенных и пользовательских Coach Pack.</summary>
public partial class CoachingLibraryView : UserControl
{
    public CoachingLibraryView()
    {
        InitializeComponent();
    }

    public event EventHandler? CreateRequested;

    public event EventHandler? ImportRequested;

    public event EventHandler<CoachingPackEventArgs>? PackOpenRequested;

    public event EventHandler<CoachingPackEventArgs>? PackExportRequested;

    public event EventHandler<CoachingPackEventArgs>? PackDeleteRequested;

    private CoachingLibraryViewModel ViewModel => (CoachingLibraryViewModel)DataContext;

    private async void CoachingLibraryView_Loaded(object sender, RoutedEventArgs e) =>
        await ViewModel.EnsureLoadedAsync();

    private void CreatePackButton_Click(object sender, RoutedEventArgs e) =>
        CreateRequested?.Invoke(this, EventArgs.Empty);

    private void ImportPackButton_Click(object sender, RoutedEventArgs e) =>
        ImportRequested?.Invoke(this, EventArgs.Empty);

    private void OpenCoachingPackButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: CoachingPackCardViewModel card })
        {
            PackOpenRequested?.Invoke(this, new CoachingPackEventArgs(card));
        }
    }

    private async void PackEnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle
            || toggle.DataContext is not CoachingPackCardViewModel card)
        {
            return;
        }

        toggle.IsEnabled = false;
        var wasSaved = await ViewModel.SetPackEnabledAsync(
            card,
            toggle.IsChecked == true);
        if (!wasSaved)
        {
            toggle.IsChecked = card.IsEnabled;
        }

        toggle.IsEnabled = true;
    }

    private async void MapControlPackEnabledToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggle
            || toggle.DataContext is not MapControlPackCardViewModel card)
        {
            return;
        }

        toggle.IsEnabled = false;
        var wasSaved = await ViewModel.SetMapControlPackEnabledAsync(
            card,
            toggle.IsChecked == true);
        if (!wasSaved)
        {
            toggle.IsChecked = card.IsEnabled;
        }

        toggle.IsEnabled = true;
    }

    private void PackActionsMenu_ExportRequested(object? sender, EventArgs e)
    {
        if (sender is CoachPackActionsMenu { DataContext: CoachingPackCardViewModel card })
        {
            PackExportRequested?.Invoke(this, new CoachingPackEventArgs(card));
        }
    }

    private void PackActionsMenu_DeleteRequested(object? sender, EventArgs e)
    {
        if (sender is CoachPackActionsMenu { DataContext: CoachingPackCardViewModel card })
        {
            PackDeleteRequested?.Invoke(this, new CoachingPackEventArgs(card));
        }
    }
}
