using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Application;
using static TempoCue.App.Tests.CoachingViewTestContext;

namespace TempoCue.App.Tests;

/// <summary>Проверяет read-only просмотр набора и экспорт предметов.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachingPackDetailsViewTests
{
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
}
