using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;
using TempoCue.Application;
using TempoCue.Core;
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
        var pack = TestCoachPackFactory.CreateDetailedMatchup() with
        {
            Source = new CoachPackSource
            {
                Kind = CoachPackSourceKind.CommunityGuide,
                Title = "Ultimate Urgot Guide",
                Author = "GuideAuthor",
                Url = "https://example.com/urgot-guide",
                Permission = CoachPackSourcePermission.Granted
            },
            PersistentTips =
            [
                new CoachPersistentTipDefinition
                {
                    Id = "tip-long-title",
                    Title = "Можешь специально стоять среди своих миньонов, провоцируя противника",
                    Message = "Вместе с тобой размен примут и союзные миньоны."
                }
            ]
        };
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
        Assert.Equal(
            new Uri("https://example.com/urgot-guide"),
            context.Coaching.Details.SourceUri);
        Assert.True(WpfTestHost.Find<TextBlock>(
            detailsPage,
            "SourceUrlTextBlock").IsVisible);
        var actionsMenu = WpfTestHost.Find<CoachPackActionsMenu>(
            detailsPage,
            "PackActionsMenu");
        var actionsButton = WpfTestHost.Find<Button>(
            actionsMenu,
            "AdditionalActionsButton");
        WpfTestHost.Click(actionsButton);
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        var actionsPopup = WpfTestHost.Find<Popup>(actionsMenu, "ActionsPopup");
        var popupContent = Assert.IsAssignableFrom<FrameworkElement>(actionsPopup.Child);
        var exportCoachPackMenuButton = WpfTestHost.Find<Button>(
            popupContent,
            "ExportMenuButton");
        Assert.True(actionsPopup.IsOpen);
        Assert.True(exportCoachPackMenuButton.IsVisible);
        Assert.True(exportCoachPackMenuButton.IsEnabled);
        Assert.Equal("Экспортировать набор в файл", exportCoachPackMenuButton.Content);
        Assert.False(WpfTestHost.Find<Button>(
            popupContent,
            "CheckUpdateMenuButton").IsEnabled);
        SaveVisualSnapshotIfRequested(popupContent, "coach-pack-actions-menu.png");
        actionsPopup.IsOpen = false;
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-overview.png");

        using var coachPackStream = new MemoryStream();
        Assert.True(context.Coaching.Details.ExportCoachPackAsync(coachPackStream)
            .GetAwaiter()
            .GetResult());
        WpfTestHost.Pump(DispatcherPriority.ApplicationIdle);
        Assert.True(context.Coaching.Details.HasCoachPackExportMessage);
        SaveVisualSnapshotIfRequested(context.Window, "coach-pack-overview-exported.png");

        WpfTestHost.Click(WpfTestHost.Find<Button>(detailsPage, "PackAdviceTabButton"));
        Assert.True(WpfTestHost.Find<Grid>(detailsPage, "PackAdviceContent").IsVisible);
        Assert.Equal(2, context.Coaching.Details.Phases.Count);
        Assert.Single(context.Coaching.Details.PersistentTips);
        detailsPage.UpdateLayout();
        WpfTestHost.Pump(DispatcherPriority.Render);
        var scopeText = Assert.Single(
            WpfTestHost.FindVisualDescendants<TextBlock>(detailsPage),
            text => text.Text is "Весь матч" or "Whole match");
        var scopeBadge = Assert.IsType<Border>(VisualTreeHelper.GetParent(scopeText));
        Assert.Equal(VerticalAlignment.Top, scopeBadge.VerticalAlignment);
        var primaryAdviceTexts = WpfTestHost.FindVisualDescendants<TextBlock>(detailsPage)
            .Where(text => text.Text == "Содержимое набора остаётся на языке автора.")
            .ToArray();
        Assert.Equal(2, primaryAdviceTexts.Length);
        Assert.All(primaryAdviceTexts, text => Assert.Equal(FontWeights.Normal, text.FontWeight));
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
