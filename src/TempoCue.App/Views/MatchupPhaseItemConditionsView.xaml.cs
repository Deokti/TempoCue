using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Выбор предметов, которые начинают или завершают редактируемую фазу.</summary>
public partial class MatchupPhaseItemConditionsView : UserControl
{
    public MatchupPhaseItemConditionsView()
    {
        InitializeComponent();
    }

    private MatchupContentViewModel ViewModel => (MatchupContentViewModel)DataContext;

    private async void RetryItemCatalogButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.RetryItemCatalogLoadAsync();

    private void SelectStartItemButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenStartItemPicker();

    private void SelectEndItemButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenEndItemPicker();

    private void RemoveStartItemButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RemoveStartItem();

    private void RemoveEndItemButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.RemoveEndItem();
}
