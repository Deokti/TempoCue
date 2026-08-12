using System.Windows;
using System.Windows.Controls;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет возврат между шагами редактора и сохранность черновика.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupEditorNavigationViewTests
{
    [Fact]
    public void BackButton_ReturnsThroughCompletedStepsAndKeepsContentDraft()
    {
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            context.Show();
            context.OpenMatchupContent();

            var content = context.Coaching.MatchupEditor.Content;
            content.BeginAddPhase();
            content.Editor.Name = "Осторожное начало";
            content.Editor.PrimaryAdvice = "Не ищи размен первым";
            Assert.True(content.SaveEditor());

            var backButton = WpfTestHost.Find<Button>(
                context.MatchupEditorPage,
                "BackToPackTypeButton");
            WpfTestHost.Click(backButton);
            Assert.Equal(2, context.Coaching.MatchupEditor.CurrentStep);
            Assert.Equal(Visibility.Visible, context.ChampionsAndRolePage.Visibility);
            Assert.Equal(Visibility.Collapsed, context.ContentPage.Visibility);
            Assert.Single(content.Phases);

            WpfTestHost.Click(backButton);
            Assert.Equal(1, context.Coaching.MatchupEditor.CurrentStep);
            Assert.Equal(Visibility.Visible, context.MainInformationPage.Visibility);
            Assert.Equal(Visibility.Collapsed, context.ChampionsAndRolePage.Visibility);
            Assert.True(backButton.IsEnabled);

            WpfTestHost.Click(backButton);
            Assert.Equal(CoachingScreen.PackTypeSelection, context.Coaching.CurrentScreen);
        });
    }

    [Fact]
    public void StepTabs_OpenPreviouslyCompletedPagesAndKeepFuturePageDisabled()
    {
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            context.Show();
            context.OpenMatchupContent();
            var steps = WpfTestHost.Find<ListBox>(
                context.MatchupEditorPage,
                "MatchupEditorStepsListBox");

            Assert.Equal(
                Visibility.Collapsed,
                WpfTestHost.Find<Button>(context.MatchupEditorPage, "CloseMatchupEditorButton").Visibility);
            Assert.False(((ListBoxItem)steps.Items[4]).IsEnabled);
            WpfTestHost.Click((ListBoxItem)steps.Items[0]);
            Assert.Equal(1, context.Coaching.MatchupEditor.CurrentStep);
            Assert.Equal(Visibility.Visible, context.MainInformationPage.Visibility);

            WpfTestHost.Click((ListBoxItem)steps.Items[2]);
            Assert.Equal(3, context.Coaching.MatchupEditor.CurrentStep);
            Assert.Equal(Visibility.Visible, context.ContentPage.Visibility);
            CoachingViewTestContext.SaveVisualSnapshotIfRequested(
                context.Window,
                "matchup-editor-clickable-steps.png");
        });
    }
}
