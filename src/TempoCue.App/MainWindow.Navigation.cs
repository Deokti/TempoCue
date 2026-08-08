using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using TempoCue.App.Settings;

namespace TempoCue.App;

/// <summary>Навигация, системные кнопки окна и переключение языка.</summary>
public partial class MainWindow
{
    private enum AppPage
    {
        Home,
        CurrentMatch,
        Settings
    }

    private AppPage _currentPage = AppPage.Home;
    private bool _isWindowInitialized;

    private void OpenHomeButton_Click(object sender, RoutedEventArgs e) =>
        NavigateTo(AppPage.Home);

    private void OpenCurrentMatchButton_Click(object sender, RoutedEventArgs e) =>
        NavigateTo(AppPage.CurrentMatch);

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e) =>
        NavigateTo(AppPage.Settings);

    /// <summary>
    /// Единственная точка переключения экранов. При добавлении страницы достаточно
    /// расширить AppPage и описать её видимость здесь.
    /// </summary>
    private void NavigateTo(AppPage page)
    {
        _currentPage = page;

        var shellIsVisible = page is AppPage.Home or AppPage.Settings;
        HomePage.Visibility = shellIsVisible ? Visibility.Visible : Visibility.Collapsed;
        MatchPage.Visibility = page == AppPage.CurrentMatch ? Visibility.Visible : Visibility.Collapsed;
        HomeContentPage.Visibility = page == AppPage.Home ? Visibility.Visible : Visibility.Collapsed;
        SettingsContentPage.Visibility = page == AppPage.Settings ? Visibility.Visible : Visibility.Collapsed;

        UpdateShellNavigation();
        UpdateTitleBarContext();
    }

    private void WindowTitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void MinimizeWindowButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeRestoreWindowButton_Click(object sender, RoutedEventArgs e) =>
        ToggleWindowState();

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        var isMaximized = WindowState == WindowState.Maximized;
        MaximizeWindowGlyph.Visibility = isMaximized ? Visibility.Collapsed : Visibility.Visible;
        RestoreWindowGlyph.Visibility = isMaximized ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseWindowButton_Click(object sender, RoutedEventArgs e) => Close();

    private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isWindowInitialized || LanguageComboBox.SelectedValue is not string languageCode)
        {
            return;
        }

        _localization.SetLanguage(languageCode);
        AppSettingsService.SaveLanguage(languageCode);
        RefreshLocalizedContent();
    }

    /// <summary>
    /// Статические подписи обновляются Binding-ами автоматически. Здесь повторно
    /// формируются только строки, в которые подставлены данные текущего матча.
    /// </summary>
    private void RefreshLocalizedContent()
    {
        SetManualSimulationEnabled(isEnabled: !_isLiveMatchConnected);

        switch (_connectionDisplayMode)
        {
            case ConnectionDisplayMode.Connected when _latestLiveSnapshot is not null:
                RenderLiveSnapshot(_latestLiveSnapshot);
                break;
            case ConnectionDisplayMode.Error:
                RenderConnectionProblem();
                break;
            default:
                RenderWaitingConnection();
                break;
        }

        if (_connectionDisplayMode != ConnectionDisplayMode.Connected)
        {
            if (_lastAdviceMatch is null)
            {
                ShowInitialWaitingAdvice();
            }
            else
            {
                ShowAdvice(_coach.GetBestAdvice(_lastAdviceMatch));
            }
        }

        UpdateTitleBarContext();
    }

    private void UpdateTitleBarContext(string? gameTimeText = null)
    {
        var pageNameKey = _currentPage switch
        {
            AppPage.CurrentMatch => "Nav.CurrentMatch",
            AppPage.Settings => "Nav.Settings",
            _ => "Nav.Home"
        };
        var pageName = _localization.Get(pageNameKey);

        TitleBarContextTextBlock.Text = gameTimeText is null
            ? $"— {pageName}"
            : $"— {pageName} · {gameTimeText}";
    }

    private void UpdateShellNavigation()
    {
        var settingsAreOpen = _currentPage == AppPage.Settings;

        HomeNavigationButton.Style = (Style)FindResource(
            settingsAreOpen ? "ShellNavButton" : "ShellNavButtonActive");
        SettingsNavigationButton.Style = (Style)FindResource(
            settingsAreOpen ? "ShellNavButtonActive" : "ShellNavButton");

        HomeNavigationIcon.Foreground = ToBrush(settingsAreOpen ? "#7F93AF" : "#50B7ED");
        SettingsNavigationIcon.Foreground = ToBrush(settingsAreOpen ? "#50B7ED" : "#7F93AF");
        HomeNavigationTextBlock.FontWeight = settingsAreOpen ? FontWeights.Normal : FontWeights.SemiBold;
        SettingsNavigationTextBlock.FontWeight = settingsAreOpen ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private static SolidColorBrush ToBrush(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));
}
