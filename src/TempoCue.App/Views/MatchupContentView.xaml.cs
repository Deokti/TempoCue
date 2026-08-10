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
