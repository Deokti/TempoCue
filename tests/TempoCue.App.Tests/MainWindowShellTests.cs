using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using TempoCue.App.Views;
using TempoCue.Application;

namespace TempoCue.App.Tests;

/// <summary>Проверяет оболочку главного окна независимо от сценариев редактора.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MainWindowShellTests
{
    [Fact]
    public void Shell_UsesWorkAreaAndNavigatesBetweenMainPages() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        context.Show();
        var window = context.Window;

        Assert.Equal("TempoCue — ассистент для League of Legends", window.Title);
        Assert.Equal(1310, window.Width);
        Assert.Equal(750, window.Height);
        Assert.Equal(1310, window.MinWidth);
        Assert.Equal(750, window.MinHeight);
        Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
        Assert.Equal(WindowStyle.None, window.WindowStyle);
        var windowChrome = WindowChrome.GetWindowChrome(window);
        Assert.NotNull(windowChrome);
        Assert.Equal(new Thickness(6), windowChrome.ResizeBorderThickness);
        Assert.Equal(new Thickness(0), windowChrome.GlassFrameThickness);
        Assert.NotNull(window.Icon);

        window.WindowState = WindowState.Maximized;
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.True(window.ActualWidth <= SystemParameters.WorkArea.Width + 1);
        Assert.True(window.ActualHeight <= SystemParameters.WorkArea.Height + 1);
        window.WindowState = WindowState.Normal;

        var homeNavigationIcon = WpfTestHost.Find<System.Windows.Shapes.Path>(
            window,
            "HomeNavigationIcon");
        Assert.NotNull(homeNavigationIcon.Data);
        Assert.IsType<StreamGeometry>(window.FindResource("Icon.NavHome"));

        var homePage = WpfTestHost.Find<HomeView>(window, "HomeContentPage");
        context.Home.SetCoachPackCount(3);
        WpfTestHost.Pump();
        Assert.Equal(
            "3",
            WpfTestHost.Find<TextBlock>(homePage, "CoachPackCountTextBlock").Text);
        Assert.Equal(
            "В разработке",
            WpfTestHost.Find<TextBlock>(homePage, "HistoryDevelopmentTextBlock").Text);
        CoachingViewTestContext.SaveVisualSnapshotIfRequested(window, "home.png");

        var coachingBadge = WpfTestHost.Find<TextBlock>(
            window,
            "CoachingNavigationBadgeTextBlock");
        Assert.Equal("1", coachingBadge.Text);
        context.CoachPackStore.AddEntry(
            TestCoachPackFactory.CreateDetailedMatchup() with
            {
                Id = "matchup.second-enabled",
                Name = "Второй включённый матчап"
            },
            LocalCoachPackState.Enabled,
            LocalCoachPackOrigin.CreatedByUser);
        context.Coaching.Library.RefreshAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        Assert.Equal(2, context.Coaching.Library.EnabledPackCount);
        Assert.Equal("2", coachingBadge.Text);

        var currentMatchPage = WpfTestHost.Find<CurrentMatchView>(
            window,
            "CurrentMatchContentPage");
        Assert.Contains(
            currentMatchPage.Resources.MergedDictionaries,
            dictionary => dictionary.Contains("ShellScrollBar"));
        var currentMatchScrollBarStyle = Assert.IsType<Style>(
            currentMatchPage.FindResource(typeof(ScrollBar)));
        Assert.NotNull(currentMatchScrollBarStyle.BasedOn);

        WpfTestHost.Click(WpfTestHost.Find<Button>(window, "CurrentMatchNavigationButton"));
        Assert.Equal(Visibility.Visible, currentMatchPage.Visibility);
        window.UpdateLayout();
        WpfTestHost.Pump(DispatcherPriority.Render);
        var currentMatchScrollBar = Assert.Single(
            WpfTestHost.FindVisualDescendants<ScrollBar>(currentMatchPage),
            scrollBar => scrollBar.Orientation == Orientation.Vertical && scrollBar.IsVisible);
        Assert.Equal(4d, currentMatchScrollBar.ActualWidth);
        var currentMatchTabs = WpfTestHost.Find<TabControl>(
            currentMatchPage,
            "CurrentSectionsTabControl");
        Assert.Equal(3, currentMatchTabs.Items.Count);
        Assert.Equal(new Thickness(1), currentMatchTabs.BorderThickness);
        Assert.Equal(306d, currentMatchTabs.MinHeight);

        WpfTestHost.Click(WpfTestHost.Find<Button>(window, "CoachingNavigationButton"));
        Assert.Equal(Visibility.Visible, context.CoachingPage.Visibility);
        context.Window.UpdateLayout();
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.True(context.CoachingPage.ActualWidth > 900);
        Assert.True(context.CoachingPage.ActualHeight > 500);
    });
}
