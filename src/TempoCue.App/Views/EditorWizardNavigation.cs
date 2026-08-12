using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Синхронизирует одинаковое WPF-поведение панелей шагов редакторов.</summary>
internal static class EditorWizardNavigation
{
    internal static bool TryGetStep(MouseButtonEventArgs eventArgs, out int step)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        step = 0;
        var item = FindStepItem(eventArgs.OriginalSource as DependencyObject);
        return item is { IsEnabled: true }
               && int.TryParse(item.Tag?.ToString(), out step);
    }

    internal static void Navigate(
        IEditorWizardViewModel viewModel,
        ListBox steps,
        ScrollViewer contentScrollViewer,
        int targetStep)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(contentScrollViewer);

        if (targetStep >= 1
            && targetStep <= viewModel.TotalSteps
            && targetStep != viewModel.CurrentStep
            && viewModel.NavigateToStep(targetStep))
        {
            ResetScroll(contentScrollViewer);
        }

        steps.SetCurrentValue(
            Selector.SelectedIndexProperty,
            viewModel.CurrentStepIndex);
    }

    internal static void ResetScroll(ScrollViewer contentScrollViewer)
    {
        ArgumentNullException.ThrowIfNull(contentScrollViewer);
        contentScrollViewer.Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(contentScrollViewer.ScrollToTop));
    }

    private static ListBoxItem? FindStepItem(DependencyObject? element)
    {
        while (element is not null and not ListBoxItem)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        return element as ListBoxItem;
    }
}
