using System.Windows;
using System.Windows.Controls;
using TempoCue.App.Views;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class CurrentMatchChampionSelectViewTests
{
    [Fact]
    public void ChampionSelect_ShowsDedicatedContentAndHidesWaitingAndMatch()
    {
        WpfTestHost.Run(() =>
        {
            var state = new ChampionSelectState(
                0,
                [
                    ChampionSelectParticipant.Visible(
                        0,
                        6,
                        PlayerPosition.Top,
                        isLocalPlayer: true,
                        RiotId.Create("Tempo Player", "TEST"))
                ]);
            using var context = new MainWindowTestContext(
                championSelectResult: ChampionSelectReadResult.Success(state));
            context.Window.Show();
            context.CurrentMatch.RefreshAsync().GetAwaiter().GetResult();
            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.Window,
                "CurrentMatchNavigationButton"));
            context.Window.UpdateLayout();
            WpfTestHost.Pump();
            var view = WpfTestHost.Find<CurrentMatchView>(
                context.Window,
                "CurrentMatchContentPage");

            Assert.Equal(
                Visibility.Visible,
                WpfTestHost.Find<ScrollViewer>(
                    view,
                    "ChampionSelectContentScrollViewer").Visibility);
            Assert.Equal(
                Visibility.Collapsed,
                WpfTestHost.Find<Border>(view, "CurrentMatchStatePanel").Visibility);
            Assert.Equal(
                Visibility.Collapsed,
                WpfTestHost.Find<ScrollViewer>(
                    view,
                    "CurrentMatchContentScrollViewer").Visibility);
        });
    }
}
