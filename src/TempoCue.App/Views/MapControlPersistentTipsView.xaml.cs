using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

public partial class MapControlPersistentTipsView : UserControl
{
    public MapControlPersistentTipsView() => InitializeComponent();

    private MapControlContentViewModel ViewModel =>
        (MapControlContentViewModel)DataContext;

    private void AddMapPersistentTipButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.PersistentTips.BeginAdd();

    private void SaveMapPersistentTipButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.PersistentTips.SaveEditor();

    private void DuplicateEditedMapPersistentTipButton_Click(
        object sender,
        RoutedEventArgs e) => ViewModel.PersistentTips.DuplicateEditor();

    private void DeleteEditedMapPersistentTipButton_Click(
        object sender,
        RoutedEventArgs e) => ViewModel.PersistentTips.DeleteEditor();

    private void EditMapPersistentTipButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlPersistentTipViewModel tip)
        {
            ViewModel.PersistentTips.BeginEdit(tip);
        }
    }

    private void DuplicateMapPersistentTipButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlPersistentTipViewModel tip)
        {
            ViewModel.PersistentTips.Duplicate(tip);
        }
    }

    private void DeleteMapPersistentTipButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlPersistentTipViewModel tip)
        {
            ViewModel.PersistentTips.Delete(tip);
        }
    }

    private void MoveMapPersistentTipUpButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlPersistentTipViewModel tip)
        {
            ViewModel.PersistentTips.Move(tip, -1);
        }
    }

    private void MoveMapPersistentTipDownButton_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is MapControlPersistentTipViewModel tip)
        {
            ViewModel.PersistentTips.Move(tip, 1);
        }
    }
}
