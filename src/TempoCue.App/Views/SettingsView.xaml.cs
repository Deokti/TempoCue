using System.Windows.Controls;
using System.Windows.Interop;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Страница пользовательских настроек TempoCue.</summary>
public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void ConfigureOverlayButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.OpenOverlaySetup();
        }
    }

    private void SaveOverlaySettingsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.SaveOverlaySettings();
        }
    }

    private void ResetOverlaySettingsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel viewModel)
        {
            viewModel.ResetOverlaySettings();
        }
    }

    private async void SelectCaptureWindowButton_Click(object sender, System.Windows.RoutedEventArgs e)
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

    private async void StopCaptureButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel { CaptureDiagnostics: { } captureDiagnostics })
        {
            await captureDiagnostics.StopAsync();
        }
    }
}
