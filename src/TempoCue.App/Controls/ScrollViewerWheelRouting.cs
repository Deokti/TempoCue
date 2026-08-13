using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TempoCue.App.Controls;

/// <summary>Передаёт колесо внешнему экрану, когда вложенному списку уже некуда прокручиваться.</summary>
public static class ScrollViewerWheelRouting
{
    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled",
        typeof(bool),
        typeof(ScrollViewerWheelRouting),
        new PropertyMetadata(false, IsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject element) =>
        (bool)element.GetValue(IsEnabledProperty);

    public static void SetIsEnabled(DependencyObject element, bool value) =>
        element.SetValue(IsEnabledProperty, value);

    private static void IsEnabledChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is not ScrollViewer scrollViewer)
        {
            throw new InvalidOperationException(
                "ScrollViewerWheelRouting можно включать только для ScrollViewer.");
        }

        scrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
        if (e.NewValue is true)
        {
            scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
        }
    }

    private static void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var owner = (ScrollViewer)sender;
        var nested = FindNearestScrollViewer(e.OriginalSource as DependencyObject);
        if (nested is not null
            && nested != owner
            && CanScrollVertically(nested, e.Delta))
        {
            return;
        }

        if (!CanScrollVertically(owner, e.Delta))
        {
            return;
        }

        owner.ScrollToVerticalOffset(owner.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private static ScrollViewer? FindNearestScrollViewer(DependencyObject? source)
    {
        for (var current = source; current is not null; current = GetParent(current))
        {
            if (current is ScrollViewer scrollViewer)
            {
                return scrollViewer;
            }
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject element) => element switch
    {
        Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(element),
        FrameworkContentElement contentElement => contentElement.Parent,
        _ => LogicalTreeHelper.GetParent(element)
    };

    private static bool CanScrollVertically(ScrollViewer scrollViewer, int delta) =>
        delta > 0
            ? scrollViewer.VerticalOffset > 0
            : delta < 0 && scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;
}
