using System.Windows.Controls;
using System.Windows.Interop;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Диагностика выбранного окна, кадров и распознавания миникарты.</summary>
public partial class CaptureDiagnosticsView : UserControl
{
    public CaptureDiagnosticsView()
    {
        InitializeComponent();
    }

    private async void SelectCaptureWindowButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        var owner = System.Windows.Window.GetWindow(this);
        if (owner is null || DataContext is not SettingsViewModel
            {
                CaptureDiagnostics: { } captureDiagnostics
            })
        {
            return;
        }

        var ownerHandle = new WindowInteropHelper(owner).Handle;
        if (ownerHandle == 0)
        {
            return;
        }

        await captureDiagnostics.SelectWindowAsync(ownerHandle);
    }

    private async void StopCaptureButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel { CaptureDiagnostics: { } captureDiagnostics })
        {
            await captureDiagnostics.StopAsync();
        }
    }

    private async void SaveReplaySampleButton_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel { CaptureDiagnostics: { } captureDiagnostics })
        {
            await MinimapReplaySampleFileDialog.ShowAsync(
                System.Windows.Window.GetWindow(this),
                captureDiagnostics);
        }
    }
}
