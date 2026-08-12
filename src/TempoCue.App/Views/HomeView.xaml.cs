using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Главная страница TempoCue.</summary>
public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    public event EventHandler? OpenCurrentMatchRequested;

    private async void CheckAgainButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel)
        {
            return;
        }

        try
        {
            await viewModel.RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            // Закрытие приложения может совпасть с ручной проверкой.
        }
    }

    private void OpenCurrentMatchButton_Click(object sender, RoutedEventArgs e) =>
        OpenCurrentMatchRequested?.Invoke(this, EventArgs.Empty);
}
