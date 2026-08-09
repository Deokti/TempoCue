using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Модальный выбор одного чемпиона из загруженного каталога.</summary>
public partial class ChampionPickerDialogView : UserControl
{
    public ChampionPickerDialogView()
    {
        InitializeComponent();
        IsVisibleChanged += ChampionPickerDialogView_IsVisibleChanged;
    }

    private MatchupChampionsAndRoleViewModel ViewModel =>
        (MatchupChampionsAndRoleViewModel)DataContext;

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ClosePicker();

    private void ChampionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ChampionOptionViewModel champion })
        {
            ViewModel.SelectPickerChampion(champion);
        }
    }

    private void ChampionPickerDialogView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel.ClosePicker();
            e.Handled = true;
        }
    }

    private void ChampionPickerDialogView_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }

        Dispatcher.BeginInvoke(
            ChampionPickerSearchTextBox.Focus,
            DispatcherPriority.Input);
    }
}
