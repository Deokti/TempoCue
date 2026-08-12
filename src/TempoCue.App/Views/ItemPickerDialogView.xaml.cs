using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Модальный выбор предмета из локализованного каталога выбранного патча.</summary>
public partial class ItemPickerDialogView : UserControl
{
    public ItemPickerDialogView()
    {
        InitializeComponent();
        IsVisibleChanged += ItemPickerDialogView_IsVisibleChanged;
    }

    private ItemPickerViewModel ViewModel => (ItemPickerViewModel)DataContext;

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.Close();

    private void ItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ItemOptionViewModel item })
        {
            ViewModel.ToggleSelection(item);
        }
    }

    private void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: ItemCategoryOptionViewModel option })
        {
            ViewModel.SelectCategory(option);
        }
    }

    private void ConfirmButton_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ConfirmSelection();

    private void ItemPickerDialogView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ViewModel.Close();
            e.Handled = true;
        }
    }

    private void ItemPickerDialogView_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (!IsVisible)
        {
            return;
        }

        // Фокус откладывается до построения визуального дерева открывшейся модалки.
        Dispatcher.BeginInvoke(ItemPickerSearchTextBox.Focus, DispatcherPriority.Input);
    }
}
