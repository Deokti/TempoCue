using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Application;
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
    public void Library_ShowsSavedMapControlPackAndPersistsToggle() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var pack = TestMapControlPackFactory.Create() with
        {
            Name = "Контроль раннего дракона",
            Author = string.Empty
        };
        context.MapControlPackStore.SaveAsync(pack, LocalCoachPackState.Enabled)
            .GetAwaiter()
            .GetResult();
        var libraryPage = OpenLibrary(context);
        var coachingTabs = WpfTestHost.Find<TabControl>(
            libraryPage,
            "CoachingSectionsTabControl");

        coachingTabs.SelectedIndex = 1;
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var card = Assert.Single(context.Coaching.Library.MapControlCards);
        Assert.Equal(pack.Id, card.Pack.Id);
        Assert.Equal("Контроль раннего дракона", card.Title);
        Assert.Contains("все роли · вы", card.Details);
        Assert.Contains("1 совет", card.AdviceCount);
        Assert.False(WpfTestHost.Find<Border>(libraryPage, "MapControlEmptyState").IsVisible);
        var toggle = Assert.Single(
            WpfTestHost.FindVisualDescendants<ToggleButton>(libraryPage),
            candidate => candidate.Name == "MapControlPackEnabledToggle");
        Assert.True(toggle.IsChecked);
        SaveVisualSnapshotIfRequested(context.Window, "coaching-library-map-control.png");

        WpfTestHost.Click(toggle);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        card = Assert.Single(context.Coaching.Library.MapControlCards);
        Assert.True(card.IsDisabled);
        Assert.False(card.IsEnabled);
    });

    [Fact]
    public void Library_MapControlCardOpensDetailsAndOffersFileActions() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        var pack = TestMapControlPackFactory.Create("map-control.details-test");
        context.MapControlPackStore.SaveAsync(pack, LocalCoachPackState.Enabled)
            .GetAwaiter()
            .GetResult();
        var libraryPage = OpenLibrary(context);
        context.Coaching.Library.RefreshAsync().GetAwaiter().GetResult();
        WpfTestHost.Find<TabControl>(libraryPage, "CoachingSectionsTabControl").SelectedIndex = 1;
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        var openButton = Assert.Single(
            WpfTestHost.FindVisualDescendants<Button>(libraryPage),
            button => button.DataContext is MapControlPackCardViewModel card
                      && card.Pack.Id == pack.Id
                      && Equals(button.Content, context.Localization.Get("Coaching.Library.Open")));
        var actionsMenu = Assert.Single(
            WpfTestHost.FindVisualDescendants<CoachPackActionsMenu>(libraryPage),
            menu => menu.DataContext is MapControlPackCardViewModel card
                    && card.Pack.Id == pack.Id);
        Assert.True(actionsMenu.CanExport);
        Assert.True(actionsMenu.CanDelete);

        WpfTestHost.Click(openButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

        Assert.Equal(CoachingScreen.MapControlPackDetails, context.Coaching.CurrentScreen);
        Assert.Equal(pack.Id, context.Coaching.MapControlDetails.Pack?.Id);
        Assert.Single(context.Coaching.MapControlDetails.Rules);
        Assert.True(context.MapControlPackDetailsPage.IsVisible);
        Assert.True(WpfTestHost.Find<Button>(
            context.MapControlPackDetailsPage,
            "MapControlPackEditActionButton").IsEnabled);
        var detailsActionsMenu = WpfTestHost.Find<CoachPackActionsMenu>(
            context.MapControlPackDetailsPage,
            "MapControlPackActionsMenu");
        Assert.True(detailsActionsMenu.CanExport);
        Assert.True(detailsActionsMenu.CanDelete);
        SaveVisualSnapshotIfRequested(
            context.Window,
            "map-control-pack-details.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(
            context.MapControlPackDetailsPage,
            "ObjectivesTabButton"));
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        Assert.Equal(MapControlPackDetailsTab.Objectives, context.Coaching.MapControlDetails.CurrentTab);
        Assert.True(WpfTestHost.Find<StackPanel>(
            context.MapControlPackDetailsPage,
            "ObjectivesPanel").IsVisible);
        SaveVisualSnapshotIfRequested(context.Window, "map-control-pack-objectives.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(
            context.MapControlPackDetailsPage,
            "TimedTabButton"));
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        Assert.Equal(MapControlPackDetailsTab.TimedReminders, context.Coaching.MapControlDetails.CurrentTab);
        Assert.True(WpfTestHost.Find<StackPanel>(
            context.MapControlPackDetailsPage,
            "TimedPanel").IsVisible);
        SaveVisualSnapshotIfRequested(context.Window, "map-control-pack-timed.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(
            context.MapControlPackDetailsPage,
            "PersistentTabButton"));
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        Assert.Equal(MapControlPackDetailsTab.PersistentTips, context.Coaching.MapControlDetails.CurrentTab);
        Assert.True(WpfTestHost.Find<StackPanel>(
            context.MapControlPackDetailsPage,
            "PersistentPanel").IsVisible);
        SaveVisualSnapshotIfRequested(context.Window, "map-control-pack-persistent.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(
            context.MapControlPackDetailsPage,
            "MapControlPackEditActionButton"));
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        Assert.Equal(CoachingScreen.MapControlEditor, context.Coaching.CurrentScreen);
        Assert.Equal(MatchupEditorMode.Edit, context.Coaching.MapControlEditor.Mode);
        var closeEditorButton = WpfTestHost.Find<Button>(
            context.MapControlEditorPage,
            "CloseMapControlEditorButton");
        Assert.Equal(Visibility.Visible, closeEditorButton.Visibility);
        SaveVisualSnapshotIfRequested(context.Window, "map-control-editor-close-action.png");

        WpfTestHost.Click(closeEditorButton);
        Assert.Equal(CoachingScreen.MapControlPackDetails, context.Coaching.CurrentScreen);
        Assert.True(context.MapControlPackDetailsPage.IsVisible);
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
    public void Library_CardActionsMenuOffersExportAndDeleteWithoutOpeningDetails() =>
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            var pack = TestCoachPackFactory.CreateDetailedMatchup() with
            {
                Id = "matchup.library-card-actions",
                Name = "Действия из карточки"
            };
            context.CoachPackStore.SaveAsync(pack, LocalCoachPackState.Disabled)
                .GetAwaiter()
                .GetResult();
            var libraryPage = OpenLibrary(context);
            context.Coaching.Library.RefreshAsync().GetAwaiter().GetResult();
            WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

            var actionsMenu = Assert.Single(
                WpfTestHost.FindVisualDescendants<CoachPackActionsMenu>(libraryPage),
                menu => menu.DataContext is CoachingPackCardViewModel card
                        && card.Pack.Id == pack.Id);
            WpfTestHost.Click(WpfTestHost.Find<Button>(
                actionsMenu,
                "AdditionalActionsButton"));
            WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

            var popup = WpfTestHost.Find<Popup>(actionsMenu, "ActionsPopup");
            var popupContent = Assert.IsAssignableFrom<FrameworkElement>(popup.Child);
            Assert.True(popup.IsOpen);
            Assert.True(WpfTestHost.Find<Button>(popupContent, "ExportMenuButton").IsEnabled);
            var deleteButton = WpfTestHost.Find<Button>(popupContent, "DeleteMenuButton");
            Assert.True(deleteButton.IsVisible);
            Assert.Equal("Удалить набор", deleteButton.Content);
            SaveVisualSnapshotIfRequested(popupContent, "coach-pack-card-actions-menu.png");
            SaveVisualSnapshotIfRequested(context.Window, "coaching-library-card-actions.png");

            WpfTestHost.Click(deleteButton);
            WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);

            Assert.Equal(CoachingScreen.Library, context.Coaching.CurrentScreen);
            Assert.True(context.Coaching.IsDeleteConfirmationOpen);
            Assert.Equal(pack.Id, context.Coaching.Details.Pack?.Id);
            Assert.Equal(
                Visibility.Visible,
                WpfTestHost.Find<Grid>(
                    context.CoachingPage,
                    "DeleteConfirmationOverlay").Visibility);
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
