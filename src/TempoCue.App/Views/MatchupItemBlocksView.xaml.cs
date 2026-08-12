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

    private MatchupContentViewModel ContentViewModel =>
        (MatchupContentViewModel)DataContext;

    private void AddItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.BeginAdd();

    private void SaveItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.SaveEditor();

    private void DuplicateEditedItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DuplicateEditor();

    private void DeleteEditedItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.DeleteEditor();

    private void SelectEditedBlockItemsButton_Click(object sender, RoutedEventArgs e) =>
        ContentViewModel.OpenEditedItemBlockPicker();

    private void MoveEditedItemLeftButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Editor.MoveItemLeft(GetItem(sender));

    private void MoveEditedItemRightButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Editor.MoveItemRight(GetItem(sender));

    private void RemoveEditedItemButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Editor.RemoveItem(GetItem(sender));

    private void EditItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.BeginEdit(GetBlock(sender));

    private void DuplicateItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Duplicate(GetBlock(sender));

    private void DeleteItemBlockButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Delete(GetBlock(sender));

    private void AddItemToBlockButton_Click(object sender, RoutedEventArgs e) =>
        ContentViewModel.OpenItemBlockPicker(GetBlock(sender));

    private void MoveItemBlockUpButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.MoveUp(GetBlock(sender));

    private void MoveItemBlockDownButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.MoveDown(GetBlock(sender));

    private void MoveSavedItemLeftButton_Click(object sender, RoutedEventArgs e)
    {
        var (block, item) = GetBlockItem(sender);
        ViewModel.MoveItemLeft(block, item);
    }

    private void MoveSavedItemRightButton_Click(object sender, RoutedEventArgs e)
    {
        var (block, item) = GetBlockItem(sender);
        ViewModel.MoveItemRight(block, item);
    }

    private void RemoveSavedItemButton_Click(object sender, RoutedEventArgs e)
    {
        var (block, item) = GetBlockItem(sender);
        ViewModel.RemoveItem(block, item);
    }

    private static MatchupItemBlockViewModel GetBlock(object sender) =>
        (MatchupItemBlockViewModel)((FrameworkElement)sender).DataContext;

    private static MatchupItemDraft GetItem(object sender) =>
        (MatchupItemDraft)((FrameworkElement)sender).DataContext;

    private static (MatchupItemBlockViewModel Block, MatchupItemDraft Item) GetBlockItem(
        object sender)
    {
        var element = (FrameworkElement)sender;
        return (
            (MatchupItemBlockViewModel)element.Tag,
            (MatchupItemDraft)element.DataContext);
    }
}
