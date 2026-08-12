using System.Windows.Controls;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class MatchupPhaseValidationViewTests
{
    [Fact]
    public void ValidationCard_ShowsCoverageAndNamesConflictingPhases()
    {
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext();
            context.Show();
            context.OpenMatchupContent();

            var content = context.Coaching.MatchupEditor.Content;
            var first = SavePhase(content, "Первая фаза");
            var validationView = WpfTestHost.Find<MatchupPhaseValidationView>(
                context.ContentPage,
                "PhaseValidationView");

            Assert.Equal(
                "Фазы согласованы",
                WpfTestHost.Find<TextBlock>(
                    validationView,
                    "ValidationStatusTitleTextBlock").Text);
            Assert.Equal(
                18,
                WpfTestHost.Find<ItemsControl>(
                    validationView,
                    "LevelCoverageItemsControl").Items.Count);

            content.DuplicatePhase(first);
            WpfTestHost.Pump();

            Assert.Equal(
                "Найдены конфликты фаз",
                WpfTestHost.Find<TextBlock>(
                    validationView,
                    "ValidationStatusTitleTextBlock").Text);
            var issue = Assert.Single(WpfTestHost.Find<ItemsControl>(
                validationView,
                "ValidationIssuesItemsControl").Items);
            var issueViewModel = Assert.IsType<MatchupPhaseValidationIssueViewModel>(issue);
            Assert.Contains("Первая фаза", issueViewModel.Message);
            Assert.Contains("Первая фаза — копия", issueViewModel.Message);
        });
    }

    private static MatchupPhaseViewModel SavePhase(
        MatchupContentViewModel content,
        string name)
    {
        content.BeginAddPhase();
        content.Editor.Name = name;
        content.Editor.PrimaryAdvice = "Проверяемый совет фазы";
        Assert.True(content.SaveEditor());
        return content.Phases[^1];
    }
}
