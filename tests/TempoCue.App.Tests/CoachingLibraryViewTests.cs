using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TempoCue.App.Views;
using static TempoCue.App.Tests.CoachingViewTestContext;

namespace TempoCue.App.Tests;

/// <summary>Проверяет поиск, разделы и карточки библиотеки коучинга.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachingLibraryViewTests
{
    [Fact]
    public void Library_ShowsResponsiveCardsAndSearchState() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var libraryPage = OpenLibrary(context);
        var matchupScrollViewer = WpfTestHost.Find<ScrollViewer>(
            libraryPage,
            "MatchupLibraryScrollViewer");

        AssertLibraryStyles(libraryPage, matchupScrollViewer);
        Assert.Single(context.Coaching.Library.MatchupCards);
        Assert.Empty(context.Coaching.Library.MyPackCards);
        Assert.Contains("1 набор советов", context.Coaching.Library.LibrarySummary);

        var searchTextBox = WpfTestHost.Find<TextBox>(libraryPage, "CoachingSearchTextBox");
        var searchPlaceholder = WpfTestHost.Find<TextBlock>(
            libraryPage,
            "CoachingSearchPlaceholder");
        Assert.Equal(Visibility.Visible, searchPlaceholder.Visibility);
        Assert.True(searchTextBox.Focus());
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.Equal(Visibility.Collapsed, searchPlaceholder.Visibility);

        AssertResponsiveLayout(context, libraryPage, matchupScrollViewer);

        context.Coaching.Library.SearchText = "Sett";
        Assert.Single(context.Coaching.Library.MatchupCards);
        context.Coaching.Library.SearchText = "неизвестный матчап";
        Assert.Empty(context.Coaching.Library.MatchupCards);
    });

    [Fact]
    public void Library_SwitchesBetweenSections() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var libraryPage = OpenLibrary(context);
        var coachingTabs = WpfTestHost.Find<TabControl>(
            libraryPage,
            "CoachingSectionsTabControl");

        Assert.Equal(3, coachingTabs.Items.Count);
        coachingTabs.SelectedIndex = 1;
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.True(WpfTestHost.Find<Border>(libraryPage, "MapControlEmptyState").IsVisible);
        coachingTabs.SelectedIndex = 2;
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.True(WpfTestHost.Find<StackPanel>(libraryPage, "MyPacksEmptyState").IsVisible);
    });

    [Fact]
    public void Library_TogglePersistsDisabledMatchupState() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var libraryPage = OpenLibrary(context);
        var toggle = Assert.Single(
            WpfTestHost.FindVisualDescendants<ToggleButton>(libraryPage),
            candidate => candidate.Name == "PackEnabledToggle");

        Assert.True(toggle.IsChecked);
        WpfTestHost.Click(toggle);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var card = Assert.Single(context.Coaching.Library.MatchupCards);
        Assert.True(card.IsDisabled);
        Assert.False(card.IsEnabled);
        Assert.Contains("0 включено", context.Coaching.Library.LibrarySummary);
        var refreshedToggle = Assert.Single(
            WpfTestHost.FindVisualDescendants<ToggleButton>(libraryPage),
            candidate => candidate.Name == "PackEnabledToggle");
        Assert.False(refreshedToggle.IsChecked);
    });

    private static void AssertLibraryStyles(
        CoachingLibraryView libraryPage,
        ScrollViewer matchupScrollViewer)
    {
        Assert.Equal(ScrollBarVisibility.Auto, matchupScrollViewer.VerticalScrollBarVisibility);
        var scrollBarStyle = Assert.IsType<Style>(
            matchupScrollViewer.FindResource("ShellScrollBar"));
        var widthSetter = Assert.Single(
            scrollBarStyle.Setters.OfType<Setter>(),
            setter => setter.Property == FrameworkElement.WidthProperty);
        Assert.Equal(4d, widthSetter.Value);

        var packCardStyle = Assert.IsType<Style>(
            libraryPage.FindResource("LibraryPackCard"));
        var packCardWidth = Assert.Single(
            packCardStyle.Setters.OfType<Setter>(),
            setter => setter.Property == FrameworkElement.WidthProperty);
        Assert.Equal(316d, packCardWidth.Value);
        var packCardMargin = Assert.Single(
            packCardStyle.Setters.OfType<Setter>(),
            setter => setter.Property == FrameworkElement.MarginProperty);
        Assert.Equal(new Thickness(0, 0, 12, 12), packCardMargin.Value);

        var coachingTabStyle = Assert.IsType<Style>(
            libraryPage.FindResource("CoachingTab"));
        var coachingTabFontSize = Assert.Single(
            coachingTabStyle.Setters.OfType<Setter>(),
            setter => setter.Property == Control.FontSizeProperty);
        Assert.Equal(15d, coachingTabFontSize.Value);
        Assert.Equal(
            24d,
            Assert.IsType<double>(libraryPage.FindResource("Typography.FontSize.PageTitle")));
    }

    private static void AssertResponsiveLayout(
        MainWindowTestContext context,
        CoachingLibraryView libraryPage,
        ScrollViewer matchupScrollViewer)
    {
        context.Window.Width = 1100;
        context.Window.Height = 700;
        context.Window.UpdateLayout();
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.True(libraryPage.ActualWidth > 0);
        Assert.Equal(0d, matchupScrollViewer.ScrollableWidth);

        context.Window.Width = 1440;
        context.Window.Height = 900;
        context.Window.UpdateLayout();
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.Equal(0d, matchupScrollViewer.ScrollableWidth);
    }
}
