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
    public void Library_OpensPackTypeSelectionAndMatchupEditor() => WpfTestHost.Run(() =>
    {
        using var context = new MainWindowTestContext();
        context.Show();
        WpfTestHost.Click(WpfTestHost.Find<Button>(context.Window, "CoachingNavigationButton"));

        var coachingPage = context.CoachingPage;
        var coachingTabs = WpfTestHost.Find<TabControl>(
            coachingPage,
            "CoachingSectionsTabControl");
        Assert.Equal(3, coachingTabs.Items.Count);
        var createPackButton = WpfTestHost.Find<Button>(coachingPage, "CreatePackButton");
        Assert.True(createPackButton.IsEnabled);
        var rulesScrollViewer = WpfTestHost.Find<ScrollViewer>(
            coachingPage,
            "CoachingRulesScrollViewer");
        Assert.Equal(ScrollBarVisibility.Auto, rulesScrollViewer.VerticalScrollBarVisibility);
        var scrollBarStyle = Assert.IsType<Style>(
            rulesScrollViewer.FindResource("ShellScrollBar"));
        var widthSetter = Assert.Single(
            scrollBarStyle.Setters.OfType<Setter>(),
            setter => setter.Property == FrameworkElement.WidthProperty);
        Assert.Equal(4d, widthSetter.Value);
        var ruleCardStyle = Assert.IsType<Style>(
            coachingPage.FindResource("CoachingRuleCard"));
        var ruleCardMargin = Assert.Single(
            ruleCardStyle.Setters.OfType<Setter>(),
            setter => setter.Property == FrameworkElement.MarginProperty);
        Assert.Equal(new Thickness(0, 0, 8, 10), ruleCardMargin.Value);
        var coachingTabStyle = Assert.IsType<Style>(
            coachingPage.FindResource("CoachingTab"));
        var coachingTabFontSize = Assert.Single(
            coachingTabStyle.Setters.OfType<Setter>(),
            setter => setter.Property == Control.FontSizeProperty);
        Assert.Equal(15d, coachingTabFontSize.Value);
        Assert.Equal(
            24d,
            Assert.IsType<double>(coachingPage.FindResource("Typography.FontSize.PageTitle")));

        coachingTabs.SelectedIndex = 1;
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.True(WpfTestHost.Find<Border>(coachingPage, "MapControlEmptyState").IsVisible);
        coachingTabs.SelectedIndex = 2;
        WpfTestHost.Pump(DispatcherPriority.Render);
        Assert.True(WpfTestHost.Find<Border>(coachingPage, "MyPacksEmptyState").IsVisible);

        coachingTabs.SelectedIndex = 0;
        WpfTestHost.Click(createPackButton);
        var libraryPanel = WpfTestHost.Find<Grid>(coachingPage, "CoachingLibraryPanel");
        var packTypePage = WpfTestHost.Find<PackTypeSelectionView>(
            coachingPage,
            "PackTypeSelectionPage");
        Assert.Equal(Visibility.Collapsed, libraryPanel.Visibility);
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

        context.Coaching.OpenPackTypeSelection();
        WpfTestHost.Pump();
        WpfTestHost.Click(WpfTestHost.Find<Button>(packTypePage, "BackToLibraryButton"));
        Assert.Equal(Visibility.Visible, libraryPanel.Visibility);
    });
}
