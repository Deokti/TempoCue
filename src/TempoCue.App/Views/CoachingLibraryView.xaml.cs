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

    private CoachingLibraryViewModel ViewModel => (CoachingLibraryViewModel)DataContext;

    private async void CoachingLibraryView_Loaded(object sender, RoutedEventArgs e) =>
        await ViewModel.EnsureLoadedAsync();

    private void CreatePackButton_Click(object sender, RoutedEventArgs e) =>
        CreateRequested?.Invoke(this, EventArgs.Empty);

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
}
