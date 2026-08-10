using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class MatchupPhaseValidationViewModelTests
{
    [Fact]
    public void OnePhaseForAllLevels_IsValidAndCompletesStep()
    {
        RunWithContent(content =>
        {
            SavePhase(content, "Весь матч", 1, null);

            Assert.Equal(MatchupPhaseValidationState.Valid, content.Validation.State);
            Assert.Empty(content.Validation.Issues);
            Assert.True(content.IsComplete);
            Assert.All(content.Validation.LevelCoverage, level =>
                Assert.Equal(MatchupLevelCoverageStatus.Covered, level.Status));
        });
    }

    [Fact]
    public void MissingLateLevels_IsWarningButDoesNotBlockDraft()
    {
        RunWithContent(content =>
        {
            SavePhase(content, "Начало линии", 1, 8);

            Assert.Equal(MatchupPhaseValidationState.Warning, content.Validation.State);
            Assert.False(content.Validation.HasErrors);
            Assert.True(content.IsComplete);
            var issue = Assert.Single(content.Validation.Issues);
            Assert.Contains("уровнях 9–18", issue.Message);
        });
    }

    [Fact]
    public void OverlappingPhases_BlockStepUntilConflictIsDeleted()
    {
        RunWithContent(content =>
        {
            var first = SavePhase(content, "Первая фаза", 1, null);
            SavePhase(content, "Вторая фаза", 1, null);

            Assert.Equal(MatchupPhaseValidationState.Error, content.Validation.State);
            Assert.True(content.Validation.HasErrors);
            Assert.False(content.IsComplete);
            Assert.Contains(
                content.Validation.Issues,
                issue => issue.Message.Contains("Первая фаза") &&
                         issue.Message.Contains("Вторая фаза"));

            Assert.True(content.DeletePhase(first));
            Assert.Equal(MatchupPhaseValidationState.Valid, content.Validation.State);
            Assert.True(content.IsComplete);
        });
    }

    [Fact]
    public async Task RecommendedItems_DoNotRestrictLevelCoverage()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var content = new MatchupContentViewModel(localization, TestItemCatalog.Ready());
            content.SetItemCatalogContext("26.15", "ru-RU");
            await content.EnsureItemCatalogLoadedAsync();
            var cleaver = content.ItemPicker.Items.Single(item => item.Id == 3071);
            var boots = content.ItemPicker.Items.Single(item => item.Id == 3047);

            SavePhase(content, "Ранняя игра", 1, 8, [cleaver, boots]);
            SavePhase(content, "Поздняя игра", 9, null, [cleaver]);

            Assert.Equal(MatchupPhaseValidationState.Valid, content.Validation.State);
            Assert.Empty(content.Validation.Issues);
            Assert.All(content.Validation.LevelCoverage, level =>
                Assert.Equal(MatchupLevelCoverageStatus.Covered, level.Status));
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    private static MatchupPhaseViewModel SavePhase(
        MatchupContentViewModel content,
        string name,
        int minimumLevel,
        int? maximumLevel,
        IReadOnlyList<ItemOptionViewModel>? recommendedItems = null)
    {
        content.BeginAddPhase();
        content.Editor.Name = name;
        content.Editor.PrimaryAdvice = "Проверяемый совет фазы";

        while (content.Editor.MinimumLevel < minimumLevel)
        {
            content.Editor.IncreaseMinimumLevel();
        }

        content.Editor.ExtendsToEnd = maximumLevel is null;
        if (maximumLevel is int lastLevel)
        {
            while (content.Editor.MaximumLevel > lastLevel)
            {
                content.Editor.DecreaseMaximumLevel();
            }
        }

        content.Editor.SetRecommendedItems(recommendedItems ?? []);

        Assert.True(content.SaveEditor());
        return content.Phases[^1];
    }

    private static void RunWithContent(Action<MatchupContentViewModel> test)
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            test(new MatchupContentViewModel(localization, TestItemCatalog.Ready()));
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }
}
