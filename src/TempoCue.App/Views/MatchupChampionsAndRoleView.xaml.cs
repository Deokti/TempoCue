using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Второй шаг редактора матчапа: чемпион игрока, противник и роль.</summary>
public partial class MatchupChampionsAndRoleView : UserControl
{
    public MatchupChampionsAndRoleView()
    {
        InitializeComponent();
        IsVisibleChanged += MatchupChampionsAndRoleView_IsVisibleChanged;
    }

    private MatchupChampionsAndRoleViewModel ViewModel =>
        (MatchupChampionsAndRoleViewModel)DataContext;

    private async void MatchupChampionsAndRoleView_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && DataContext is MatchupChampionsAndRoleViewModel viewModel)
        {
            await viewModel.EnsureCatalogLoadedAsync();
        }
    }

    private async void RetryChampionCatalogButton_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.RetryCatalogLoadAsync();

    private void ChangePlayerChampionButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenPlayerPicker();

    private void ChangeOpponentChampionButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.OpenOpponentPicker();
}
