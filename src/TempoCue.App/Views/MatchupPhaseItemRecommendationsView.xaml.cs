using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Выбор предметных ориентиров, которые автор рекомендует для фазы.</summary>
public partial class MatchupPhaseItemRecommendationsView : UserControl
{
    public MatchupPhaseItemRecommendationsView()
    {
        InitializeComponent();
    }

    private MatchupContentViewModel ViewModel => (MatchupContentViewModel)DataContext;

    private async void RetryItemCatalogButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.RetryItemCatalogLoadAsync();

    private void SelectRecommendedItemsButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenRecommendedItemPicker();

    private void RemoveRecommendedItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: MatchupItemDraft item })
        {
            ViewModel.RemoveRecommendedItem(item);
        }
    }
}
