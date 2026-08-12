using System.Windows;
using System.Windows.Controls;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

/// <summary>Проверяет пользовательский сценарий вкладки постоянных подсказок.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupPersistentTipsViewTests
{
    [Fact]
    public void PersistentTipsView_AddsTipAndKeepsItWhenSwitchingSections()
    {
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            context.Show();
            context.OpenMatchupContent();

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.ContentPage,
                "MatchupPersistentTipsTabButton"));
            Assert.Equal(Visibility.Visible, context.PersistentTipsView.Visibility);

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.PersistentTipsView,
                "AddPersistentTipButton"));
            var tips = context.Coaching.MatchupEditor.Content.PersistentTips;
            tips.Editor.Title = "Следи за шкалой стойкости";
            tips.Editor.Text = "Не используй ультимейт, пока шкала стойкости Сетта заполнена.";
            WpfTestHost.Pump();

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.PersistentTipsView,
                "SavePersistentTipButton"));
            Assert.Single(tips.Tips);
            Assert.Equal("Постоянные подсказки 1", context.Coaching.MatchupEditor.Content.PersistentTipsTabLabel);

            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.ContentPage,
                "MatchupPhasesTabButton"));
            WpfTestHost.Click(WpfTestHost.Find<Button>(
                context.ContentPage,
                "MatchupPersistentTipsTabButton"));

            Assert.Single(tips.Tips);
            Assert.Equal(Visibility.Visible, context.PersistentTipsView.Visibility);
        });
    }
}
