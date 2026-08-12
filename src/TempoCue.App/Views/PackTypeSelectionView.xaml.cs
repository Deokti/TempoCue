using System.Windows;
using System.Windows.Controls;

namespace TempoCue.App.Views;

/// <summary>Выбор сценария создания нового Coach Pack.</summary>
public partial class PackTypeSelectionView : UserControl
{
    public PackTypeSelectionView()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

    public event EventHandler? MatchupSelected;

    public event EventHandler? GameControlSelected;

    // Представление сообщает о выборе родительскому экрану, а маршрут определяет CoachingViewModel.
    private void BackToLibraryButton_Click(object sender, RoutedEventArgs e) =>
        BackRequested?.Invoke(this, EventArgs.Empty);

    private void MatchupPackTypeButton_Click(object sender, RoutedEventArgs e) =>
        MatchupSelected?.Invoke(this, EventArgs.Empty);

    private void GameControlPackTypeButton_Click(object sender, RoutedEventArgs e) =>
        GameControlSelected?.Invoke(this, EventArgs.Empty);
}
