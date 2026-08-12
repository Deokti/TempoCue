using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Каркас последовательного редактора контроля карты.</summary>
public partial class MapControlEditorView : UserControl
{
    public MapControlEditorView()
    {
        InitializeComponent();
    }

    public event EventHandler? BackRequested;

    private MapControlEditorViewModel ViewModel =>
        (MapControlEditorViewModel)DataContext;

    private void MapControlEditorBackButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.MovePrevious())
        {
            ResetContentScroll();
        }
        else
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void MapControlEditorNextButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.MoveNext())
        {
            ResetContentScroll();
        }
    }

    private void MapControlEditorStepsListBox_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        NavigateToStep(MapControlEditorStepsListBox.SelectedIndex + 1);

    private void MapControlEditorStepsListBox_PreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (FindStepItem(e.OriginalSource as DependencyObject) is not { IsEnabled: true } stepItem
            || !int.TryParse(stepItem.Tag?.ToString(), out var selectedStep))
        {
            return;
        }

        NavigateToStep(selectedStep);
        e.Handled = true;
    }

    private void NavigateToStep(int selectedStep)
    {
        if (selectedStep < 1
            || selectedStep > ViewModel.TotalSteps
            || selectedStep == ViewModel.CurrentStep)
        {
            return;
        }

        if (ViewModel.NavigateToStep(selectedStep))
        {
            ResetContentScroll();
        }

        MapControlEditorStepsListBox.SetCurrentValue(
            Selector.SelectedIndexProperty,
            ViewModel.CurrentStepIndex);
    }

    private static ListBoxItem? FindStepItem(DependencyObject? element)
    {
        while (element is not null and not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return element as ListBoxItem;
    }

    private void ResetContentScroll() =>
        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(MapControlEditorContentScrollViewer.ScrollToTop));
}
