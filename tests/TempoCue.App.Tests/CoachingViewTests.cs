using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Application;

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

    [Fact]
    public void Library_OpensPackDetailsTabsAndExportPreview() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var pack = TestCoachPackFactory.CreateDetailedMatchup();
        context.CoachPackStore.SaveAsync(pack, LocalCoachPackState.Enabled)
            .GetAwaiter()
            .GetResult();
        var libraryPage = OpenLibrary(context);
        context.Coaching.Library.RefreshAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var openButton = Assert.Single(
            WpfTestHost.FindVisualDescendants<Button>(libraryPage),
            button => button.Name == "OpenCoachingPackButton"
                      && button.DataContext is CoachingPackCardViewModel card
                      && card.Pack.Id == pack.Id);
        WpfTestHost.Click(openButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var detailsPage = context.CoachingPackDetailsPage;
        Assert.Equal(Visibility.Visible, detailsPage.Visibility);
        Assert.True(WpfTestHost.Find<Grid>(detailsPage, "PackOverviewContent").IsVisible);
        Assert.Equal("Urgot против Sett", context.Coaching.Details.Title);
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-overview.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(detailsPage, "PackAdviceTabButton"));
        Assert.True(WpfTestHost.Find<Grid>(detailsPage, "PackAdviceContent").IsVisible);
        Assert.Equal(2, context.Coaching.Details.Phases.Count);
        Assert.Single(context.Coaching.Details.PersistentTips);
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-advice.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(detailsPage, "PackItemsTabButton"));
        Assert.True(WpfTestHost.Find<StackPanel>(detailsPage, "PackItemsContent").IsVisible);
        Assert.Equal(2, context.Coaching.Details.ItemBlocks.Count);
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-items.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(detailsPage, "ExportToLeagueButton"));
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        var exportDialog = WpfTestHost.Find<LeagueItemSetExportDialogView>(
            detailsPage,
            "LeagueItemSetExportDialog");
        Assert.True(exportDialog.IsVisible);
        Assert.True(context.Coaching.Details.Export.CanExport);
        Assert.Contains("\"associatedChampions\": [", context.Coaching.Details.Export.Json);
        Assert.Contains("\"id\": \"3071\"", context.Coaching.Details.Export.Json);
        Assert.Contains("Ключевая сборка", context.Coaching.Details.Export.Json);
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-export.png");

        context.Coaching.Details.Export.Close();
        context.Localization.SetLanguage("en-US");
        context.Coaching.Details.ShowOverview();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        Assert.Equal("Enabled", context.Coaching.Details.StateLabel);
        Assert.StartsWith("Matchup", context.Coaching.Details.HeaderDetails);
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-overview-en.png");
    });

    private static CoachingLibraryView OpenLibrary(MainWindowTestContext context)
    {
        context.Show();
        WpfTestHost.Click(WpfTestHost.Find<Button>(context.Window, "CoachingNavigationButton"));
        context.Coaching.Library.EnsureLoadedAsync().GetAwaiter().GetResult();
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        return context.CoachingLibraryPage;
    }

    private static void SaveVisualSnapshotIfRequested(Window window, string fileName)
    {
        var directory = Environment.GetEnvironmentVariable("TEMPOCUE_UI_SNAPSHOT_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            WpfTestHost.SavePng(window, Path.Combine(directory, fileName));
        }
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
