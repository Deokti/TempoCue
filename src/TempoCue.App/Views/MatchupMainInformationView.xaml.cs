using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Первый шаг редактора матчапа: основные метаданные набора.</summary>
public partial class MatchupMainInformationView : UserControl
{
    public MatchupMainInformationView()
    {
        InitializeComponent();
        IsVisibleChanged += MatchupMainInformationView_IsVisibleChanged;
    }

    private async void MatchupMainInformationView_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible && DataContext is MatchupMainInformationViewModel viewModel)
        {
            await viewModel.EnsureGamePatchCatalogLoadedAsync();
        }
    }

    private async void RetryGamePatchCatalogButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MatchupMainInformationViewModel viewModel)
        {
            await viewModel.RetryGamePatchCatalogLoadAsync();
        }
    }
}
