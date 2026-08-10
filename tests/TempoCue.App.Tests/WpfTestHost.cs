using System.Runtime.ExceptionServices;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    public static void Click(ButtonBase button, DispatcherPriority priority = DispatcherPriority.DataBind)
    {
        ArgumentNullException.ThrowIfNull(button);
        if (button is ToggleButton toggle)
        {
            toggle.IsChecked = !(toggle.IsChecked ?? false);
        }

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

    /// <summary>Сохраняет фактически отрисованный WPF-элемент для ручной сверки с макетом.</summary>
    public static void SavePng(FrameworkElement element, string filePath)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        element.UpdateLayout();
        Pump(DispatcherPriority.Render);
        var dpi = VisualTreeHelper.GetDpi(element);
        var width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX));
        var height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY));
        var bitmap = new RenderTargetBitmap(
            width,
            height,
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(element);

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }
}
