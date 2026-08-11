using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace TempoCue.App;

/// <summary>Навигация, системные кнопки окна и переключение языка.</summary>
public partial class MainWindow
{
    private static readonly SolidColorBrush _navigationActiveBrush =
        CreateFrozenBrush("#50B7ED");
    private static readonly SolidColorBrush _navigationInactiveBrush =
        CreateFrozenBrush("#7F93AF");
    private static readonly SolidColorBrush _waitingBadgeBackgroundBrush =
        CreateFrozenBrush("#1E2E45");
    private static readonly SolidColorBrush _waitingStatusDotBrush =
        CreateFrozenBrush("#8393A8");
    private static readonly SolidColorBrush _waitingStatusTextBrush =
        CreateFrozenBrush("#B5C5D8");
    private static readonly SolidColorBrush _connectedBadgeBackgroundBrush =
        CreateFrozenBrush("#17342D");
    private static readonly SolidColorBrush _connectedStatusDotBrush =
        CreateFrozenBrush("#64D5A4");
    private static readonly SolidColorBrush _connectedStatusTextBrush =
        CreateFrozenBrush("#87E1BB");
    private static readonly SolidColorBrush _errorBadgeBackgroundBrush =
        CreateFrozenBrush("#3A3020");
    private static readonly SolidColorBrush _errorStatusDotBrush =
        CreateFrozenBrush("#E8BB58");
    private static readonly SolidColorBrush _errorStatusTextBrush =
        CreateFrozenBrush("#F0CC7A");

    private enum AppPage
    {
        Home,
        CurrentMatch,
        Coaching,
        Settings
    }

    private AppPage _currentPage = AppPage.Home;

    private void OpenHomeButton_Click(object sender, RoutedEventArgs e) =>
        NavigateTo(AppPage.Home);

    private void OpenCurrentMatchButton_Click(object sender, RoutedEventArgs e) =>
        NavigateTo(AppPage.CurrentMatch);

    private void OpenCoachingButton_Click(object sender, RoutedEventArgs e) =>
        NavigateTo(AppPage.Coaching);

    private void OpenSettingsButton_Click(object sender, RoutedEventArgs e) =>
        NavigateTo(AppPage.Settings);

    /// <summary>
    /// Единственная точка переключения экранов. При добавлении страницы достаточно
    /// расширить AppPage и описать её видимость здесь.
    /// </summary>
    private void NavigateTo(AppPage page)
    {
        _currentPage = page;

        HomePage.Visibility = Visibility.Visible;
        HomeContentPage.Visibility = page == AppPage.Home ? Visibility.Visible : Visibility.Collapsed;
        CurrentMatchContentPage.Visibility = page == AppPage.CurrentMatch
            ? Visibility.Visible
            : Visibility.Collapsed;
        CoachingContentPage.Visibility = page == AppPage.Coaching
            ? Visibility.Visible
            : Visibility.Collapsed;
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

    private void UpdateTitleBarContext(string? gameTimeText = null)
    {
        var pageNameKey = _currentPage switch
        {
            AppPage.CurrentMatch => "Nav.CurrentMatch",
            AppPage.Coaching => "Nav.Coaching",
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
        var homeIsOpen = _currentPage == AppPage.Home;
        var matchIsOpen = _currentPage == AppPage.CurrentMatch;
        var coachingIsOpen = _currentPage == AppPage.Coaching;
        var settingsAreOpen = _currentPage == AppPage.Settings;

        HomeNavigationButton.Style = (Style)FindResource(
            homeIsOpen ? "ShellNavButtonActive" : "ShellNavButton");
        CurrentMatchNavigationButton.Style = (Style)FindResource(
            matchIsOpen ? "ShellNavButtonActive" : "ShellNavButton");
        CoachingNavigationButton.Style = (Style)FindResource(
            coachingIsOpen ? "ShellNavButtonActive" : "ShellNavButton");
        SettingsNavigationButton.Style = (Style)FindResource(
            settingsAreOpen ? "ShellNavButtonActive" : "ShellNavButton");

        HomeNavigationIcon.Stroke = homeIsOpen ? _navigationActiveBrush : _navigationInactiveBrush;
        CurrentMatchNavigationIcon.Stroke = matchIsOpen
            ? _navigationActiveBrush
            : _navigationInactiveBrush;
        CoachingNavigationIcon.Stroke = coachingIsOpen
            ? _navigationActiveBrush
            : _navigationInactiveBrush;
        SettingsNavigationIcon.Stroke = settingsAreOpen
            ? _navigationActiveBrush
            : _navigationInactiveBrush;
        HomeNavigationTextBlock.FontWeight = homeIsOpen ? FontWeights.SemiBold : FontWeights.Normal;
        CurrentMatchNavigationTextBlock.FontWeight = matchIsOpen
            ? FontWeights.SemiBold
            : FontWeights.Normal;
        CoachingNavigationTextBlock.FontWeight = coachingIsOpen
            ? FontWeights.SemiBold
            : FontWeights.Normal;
        SettingsNavigationTextBlock.FontWeight = settingsAreOpen ? FontWeights.SemiBold : FontWeights.Normal;
    }

    private static SolidColorBrush CreateFrozenBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
