using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class MatchupVerificationViewTests
{
    [Fact]
    public void VerificationStep_PreviewsAdviceAndChangesPhaseByLevel()
    {
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            context.Show();
            context.OpenMatchupContent();
            context.AddPhase(
                "Осторожное начало",
                1,
                8,
                "Не ищи размен первым",
                PowerState.Dangerous,
                "Sett сильнее на первых уровнях",
                "Дожить до девятого уровня");
            context.AddPhase(
                "Можно давить",
                9,
                null,
                "Играй активнее",
                PowerState.Playable);

            var editorPage = context.MatchupEditorPage;
            var nextButton = WpfTestHost.Find<Button>(editorPage, "MatchupEditorNextButton");
            Assert.True(nextButton.IsEnabled);
            WpfTestHost.Click(nextButton);
            WpfTestHost.Pump();

            var page = context.VerificationPage;
            Assert.Equal(4, context.Coaching.MatchupEditor.CurrentStep);
            Assert.Equal(Visibility.Visible, page.Visibility);
            Assert.Equal(Visibility.Collapsed, context.ContentPage.Visibility);
            Assert.Equal(
                "Шаг 4 из 5 · Проверка",
                WpfTestHost.Find<TextBlock>(editorPage, "MatchupEditorStepSummaryTextBlock").Text);
            Assert.Equal(
                "Активна фаза 1: Осторожное начало",
                WpfTestHost.Find<TextBlock>(page, "VerificationActivePhaseTitleTextBlock").Text);
            Assert.Equal(
                "Не ищи размен первым",
                WpfTestHost.Find<TextBlock>(page, "VerificationPrimaryAdviceTextBlock").Text);

            context.Window.UpdateLayout();
            Assert.Equal(
                [1, 9],
                context.Coaching.MatchupEditor.Verification.Presets.Select(preset => preset.Level));
            var presets = WpfTestHost.Find<ItemsControl>(page, "VerificationPresetItemsControl");
            var levelNineButton = WpfTestHost.FindVisualDescendants<Button>(presets)
                .Single(button => button.DataContext is MatchupVerificationPresetViewModel { Level: 9 });
            WpfTestHost.Click(levelNineButton);
            Assert.Equal(9, context.Coaching.MatchupEditor.Verification.SelectedLevel);
            Assert.Equal(
                "Активна фаза 2: Можно давить",
                WpfTestHost.Find<TextBlock>(page, "VerificationActivePhaseTitleTextBlock").Text);
            Assert.True(nextButton.IsEnabled);

            context.Window.Width = 1100;
            context.Window.Height = 700;
            context.Window.UpdateLayout();
            Assert.True(page.ActualWidth > 0);
            context.Window.Width = 1440;
            context.Window.Height = 900;
            context.Window.UpdateLayout();
            Assert.True(page.ActualHeight > 0);
        });
    }
}
