using System.Runtime.ExceptionServices;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TempoCue.Overlay.Tests;

/// <summary>Выполняет проверки окна оверлея в отдельном STA-потоке.</summary>
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

    public static void Pump(DispatcherPriority priority = DispatcherPriority.Render) =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, priority);

    public static void SavePng(FrameworkElement element, string filePath)
    {
        ArgumentNullException.ThrowIfNull(element);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        element.UpdateLayout();
        Pump();
        var dpi = VisualTreeHelper.GetDpi(element);
        var bitmap = new RenderTargetBitmap(
            Math.Max(1, (int)Math.Ceiling(element.ActualWidth * dpi.DpiScaleX)),
            Math.Max(1, (int)Math.Ceiling(element.ActualHeight * dpi.DpiScaleY)),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);
        bitmap.Render(element);

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }
}
