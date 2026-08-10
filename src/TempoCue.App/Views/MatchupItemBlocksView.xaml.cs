using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Редактор и упорядоченный список произвольных блоков предметов.</summary>
public partial class MatchupItemBlocksView : UserControl
{
    public MatchupItemBlocksView()
    {
        InitializeComponent();
    }

    private MatchupItemBlocksViewModel ViewModel =>
        ((MatchupContentViewModel)DataContext).ItemBlocks;

    private void AddItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.BeginAdd();

    private void SaveItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SaveEditor();

    private void DuplicateEditedItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DuplicateEditor();

    private void DeleteEditedItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DeleteEditor();

    private void EditItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.BeginEdit(GetBlock(sender));

    private void DuplicateItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Duplicate(GetBlock(sender));

    private void DeleteItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Delete(GetBlock(sender));

    private void MoveItemBlockUpButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.MoveUp(GetBlock(sender));

    private void MoveItemBlockDownButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.MoveDown(GetBlock(sender));

    private static MatchupItemBlockViewModel GetBlock(object sender) =>
        (MatchupItemBlockViewModel)((FrameworkElement)sender).DataContext;
}
