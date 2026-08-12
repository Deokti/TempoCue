using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class MatchupSaveViewTests
{
    [Fact]
    public void SaveStep_CreatesCompleteEnabledCoachPack()
    {
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            context.Show();
            context.OpenSaveStep();

            var page = context.SavePage;
            var editor = context.MatchupEditorPage;
            Assert.Equal(5, context.Coaching.MatchupEditor.CurrentStep);
            Assert.Equal(Visibility.Visible, page.Visibility);
            Assert.Equal(
                "Шаг 5 из 5 · Сохранение",
                WpfTestHost.Find<TextBlock>(editor, "MatchupEditorStepSummaryTextBlock").Text);
            Assert.Equal(
                "2 фазы заполнены",
                context.Coaching.MatchupEditor.Save.PhaseCountTitle);

            context.Window.Width = 1100;
            context.Window.Height = 700;
            context.Window.UpdateLayout();
            Assert.True(page.ActualWidth > 0);
            context.Window.Width = 1440;
            context.Window.Height = 900;
            context.Window.UpdateLayout();
            Assert.True(page.ActualHeight > 0);

            var saveButton = WpfTestHost.Find<Button>(editor, "SaveAndEnableButton");
            Assert.True(saveButton.IsEnabled);
            WpfTestHost.Click(saveButton);
            WpfTestHost.Pump();

            var savedPack = Assert.IsType<CoachPack>(context.CoachPackStore.SavedPack);
            Assert.Equal(LocalCoachPackState.Enabled, context.CoachPackStore.SavedState);
            Assert.Equal(2, savedPack.SchemaVersion);
            Assert.Equal("Urgot", savedPack.PlayerChampion);
            Assert.Equal("Sett", savedPack.OpponentChampion);
            Assert.Equal(2, savedPack.Rules.Count);
            Assert.All(savedPack.Rules, rule => Assert.Empty(rule.RequiredItemIds));
            Assert.Equal(CoachPackSourceKind.PersonalExperience, savedPack.Source?.Kind);
            Assert.Contains("C:\\Test\\", context.Coaching.MatchupEditor.Save.SavedPath);
            Assert.Equal("Включён", context.Coaching.MatchupEditor.Save.PackStateLabel);
            Assert.Equal(CoachingScreen.Library, context.Coaching.CurrentScreen);
            Assert.Equal(Visibility.Collapsed, editor.Visibility);
            Assert.Equal(2, context.Coaching.Library.MatchupCards.Count);
            var createdCard = Assert.Single(context.Coaching.Library.MyPackCards);
            Assert.Equal(savedPack.Id, createdCard.Pack.Id);
            Assert.True(createdCard.IsEnabled);
            Assert.Contains("Urgot", createdCard.Title);
            Assert.Contains("Sett", createdCard.Title);
        });
    }

    [Fact]
    public void ExternalSource_RequiresValidMetadataButNotPermissionForLocalSave()
    {
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            context.Show();
            context.OpenSaveStep();
            var page = context.SavePage;
            var save = context.Coaching.MatchupEditor.Save;

            WpfTestHost.Click(WpfTestHost.Find<Button>(page, "GuideSourceButton"));
            Assert.False(save.CanSave);
            Assert.EndsWith("*", WpfTestHost.Find<TextBlock>(page, "SourceTitleLabel").Text);
            Assert.EndsWith("*", WpfTestHost.Find<TextBlock>(page, "SourceAuthorLabel").Text);
            Assert.EndsWith("*", WpfTestHost.Find<TextBlock>(page, "SourceUrlLabel").Text);
            Assert.Contains(
                "Обязательные поля",
                WpfTestHost.Find<TextBlock>(page, "RequiredSourceFieldsHint").Text);
            save.SourceTitle = "Ultimate Urgot Guide";
            save.SourceAuthor = "GuideAuthor";
            save.SourceUrl = "https://example.com/urgot";
            WpfTestHost.Pump();

            Assert.True(save.CanSave);
            Assert.False(save.AuthorPermissionGranted);
            Assert.Contains("Локальное сохранение доступно", save.PermissionDescription);
        });
    }
}
