using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TempoCue.App.Localization;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Views;

/// <summary>Представление текущего матча; бизнес-логика находится в ViewModel.</summary>
public partial class CurrentMatchView : UserControl
{
    public CurrentMatchView()
    {
        InitializeComponent();
    }

    private async void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CurrentMatchViewModel viewModel
            || sender is not Button button)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            await viewModel.RefreshAsync();
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void ProfileLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        if (!string.Equals(e.Uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                e.Uri.Host,
                "www.leagueofgraphs.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
        }
        catch (Exception exception) when (exception is Win32Exception
            or InvalidOperationException)
        {
            Trace.TraceWarning(
                "Не удалось открыть профиль LeagueOfGraphs: {0}",
                exception.GetType().Name);
            MessageBox.Show(
                Window.GetWindow(this),
                LocalizationService.Instance.Get("Current.Participant.ProfileOpenError.Message"),
                LocalizationService.Instance.Get("Current.Participant.ProfileOpenError.Title"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }
}
