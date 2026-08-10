using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TempoCue.App.Tests;

/// <summary>Запускает UI-проверки в отдельном STA-потоке и предоставляет общие действия WPF.</summary>
internal static class WpfTestHost
{
    public static void Run(Action testAction)
    {
        ArgumentNullException.ThrowIfNull(testAction);
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                testAction();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    public static void Pump(DispatcherPriority priority = DispatcherPriority.DataBind) =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, priority);

    public static void Click(Button button, DispatcherPriority priority = DispatcherPriority.DataBind)
    {
        ArgumentNullException.ThrowIfNull(button);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Pump(priority);
    }

    public static T Find<T>(FrameworkElement owner, string name)
        where T : FrameworkElement =>
        Assert.IsType<T>(owner.FindName(name));

    public static IEnumerable<T> FindVisualDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
