using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Компонует фазы и остальные разделы содержания матчапа.</summary>
public partial class MatchupContentView : UserControl
{
    public MatchupContentView()
    {
        InitializeComponent();
        IsVisibleChanged += MatchupContentView_IsVisibleChanged;
    }

    private MatchupContentViewModel ViewModel => (MatchupContentViewModel)DataContext;

    private void MatchupPhasesTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowPhases();

    private void MatchupPersistentTipsTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowPersistentTips();

    private void MatchupItemsTabButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ShowItems();

    // Каталог загружается при входе на шаг, но ошибка не мешает создавать фазу без предметов.
    private async void MatchupContentView_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && DataContext is MatchupContentViewModel viewModel)
        {
            await viewModel.EnsureItemCatalogLoadedAsync();
        }
    }
}
