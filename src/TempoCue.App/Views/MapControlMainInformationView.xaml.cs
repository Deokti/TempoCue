using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Первый шаг редактора контроля карты.</summary>
public partial class MapControlMainInformationView : UserControl
{
    public MapControlMainInformationView()
    {
        InitializeComponent();
    }

    private MapControlMainInformationViewModel ViewModel =>
        (MapControlMainInformationViewModel)DataContext;

    private async void RetryMapControlPatchCatalogButton_Click(
        object sender,
        RoutedEventArgs e) =>
        await ViewModel.RetryGamePatchCatalogLoadAsync();
}
