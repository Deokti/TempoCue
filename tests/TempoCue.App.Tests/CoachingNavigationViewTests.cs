using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.Views;
using static TempoCue.App.Tests.CoachingViewTestContext;

namespace TempoCue.App.Tests;

/// <summary>Проверяет переходы от библиотеки к выбору типа и редактору.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachingNavigationViewTests
{
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
        var stepItems = steps.Items.Cast<ListBoxItem>().ToArray();
        Assert.All(stepItems, item => Assert.True(item.IsHitTestVisible));
        Assert.True(stepItems[0].IsEnabled);
        Assert.All(stepItems[1..], item => Assert.False(item.IsEnabled));
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
