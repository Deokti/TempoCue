using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

/// <summary>Проверяет библиотеку коучинга и вход в редактор набора.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachingViewTests
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
        Assert.Single(context.Coaching.MatchupCards);
        Assert.Empty(context.Coaching.MyPackCards);
        Assert.Contains("1 набор советов", context.Coaching.LibrarySummary);

        var searchTextBox = WpfTestHost.Find<TextBox>(libraryPage, "CoachingSearchTextBox");
        var searchPlaceholder = WpfTestHost.Find<TextBlock>(
            libraryPage,
            "CoachingSearchPlaceholder");
        Assert.Equal(Visibility.Visible, searchPlaceholder.Visibility);
        Assert.True(searchTextBox.Focus());
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.Equal(Visibility.Collapsed, searchPlaceholder.Visibility);

        AssertResponsiveLayout(context, libraryPage, matchupScrollViewer);

        context.Coaching.SearchText = "Sett";
        Assert.Single(context.Coaching.MatchupCards);
        context.Coaching.SearchText = "неизвестный матчап";
        Assert.Empty(context.Coaching.MatchupCards);
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

        var card = Assert.Single(context.Coaching.MatchupCards);
        Assert.True(card.IsDisabled);
        Assert.False(card.IsEnabled);
        Assert.Contains("0 включено", context.Coaching.LibrarySummary);
        var refreshedToggle = Assert.Single(
            WpfTestHost.FindVisualDescendants<ToggleButton>(libraryPage),
            candidate => candidate.Name == "PackEnabledToggle");
        Assert.False(refreshedToggle.IsChecked);
    });

    [Fact]
    public void Library_OpensPackTypeSelectionAndMatchupEditor() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var libraryPage = OpenLibrary(context);
        var coachingPage = context.CoachingPage;
        var createPackButton = WpfTestHost.Find<Button>(libraryPage, "CreatePackButton");

        Assert.True(createPackButton.IsEnabled);
        WpfTestHost.Click(createPackButton);
        var packTypePage = WpfTestHost.Find<PackTypeSelectionView>(
            coachingPage,
            "PackTypeSelectionPage");
        Assert.Equal(Visibility.Collapsed, libraryPage.Visibility);
        Assert.Equal(Visibility.Visible, packTypePage.Visibility);
        var matchupPackTypeButton = WpfTestHost.Find<Button>(
            packTypePage,
            "MatchupPackTypeButton");
        Assert.True(matchupPackTypeButton.IsEnabled);
        Assert.False(WpfTestHost.Find<Button>(
            packTypePage,
            "GameControlPackTypeButton").IsEnabled);

        WpfTestHost.Click(matchupPackTypeButton);
        Assert.Equal(Visibility.Collapsed, packTypePage.Visibility);
        Assert.Equal(Visibility.Visible, context.MatchupEditorPage.Visibility);
        AssertEditorLayout(context);

        context.Coaching.OpenPackTypeSelection();
        WpfTestHost.Pump();
        WpfTestHost.Click(WpfTestHost.Find<Button>(packTypePage, "BackToLibraryButton"));
        Assert.Equal(Visibility.Visible, libraryPage.Visibility);
    });

    private static CoachingLibraryView OpenLibrary(MainWindowTestContext context)
    {
        context.Show();
        WpfTestHost.Click(WpfTestHost.Find<Button>(context.Window, "CoachingNavigationButton"));
        context.Coaching.EnsureLibraryLoadedAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        return context.CoachingLibraryPage;
    }

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

    private static void AssertEditorLayout(MainWindowTestContext context)
    {
        Assert.Equal(
            24d,
            WpfTestHost.Find<TextBlock>(
                context.MatchupEditorPage,
                "MatchupEditorTitleTextBlock").FontSize);
        var stepsPanel = WpfTestHost.Find<Border>(
            context.MatchupEditorPage,
            "MatchupEditorStepsPanel");
        Assert.Equal(new CornerRadius(12), stepsPanel.CornerRadius);
        Assert.Equal(new Thickness(12, 10, 8, 8), stepsPanel.Padding);
        var steps = WpfTestHost.Find<ListBox>(
            context.MatchupEditorPage,
            "MatchupEditorStepsListBox");
        Assert.Equal(5, steps.Items.Count);
        Assert.Equal(0, steps.SelectedIndex);
        Assert.True(steps.IsHitTestVisible);
        Assert.All(steps.Items.Cast<ListBoxItem>(), item => Assert.False(item.IsHitTestVisible));
        Assert.False(ScrollViewer.GetCanContentScroll(steps));
        context.Window.UpdateLayout();
        WpfTestHost.Pump(DispatcherPriority.Render);
        var itemsPanel = Assert.Single(
            WpfTestHost.FindVisualDescendants<StackPanel>(steps),
            panel => panel.IsItemsHost);
        Assert.Equal(new Thickness(0, 0, 0, 8), itemsPanel.Margin);
        var stepsScrollViewer = Assert.Single(
            WpfTestHost.FindVisualDescendants<ScrollViewer>(steps));
        Assert.Equal(0d, stepsScrollViewer.ScrollableWidth);
    }
}
