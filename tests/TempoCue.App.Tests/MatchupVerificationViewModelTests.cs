using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Core;

namespace TempoCue.App.Tests;

[Collection(LocalizationCollection.Name)]
public sealed class MatchupVerificationViewModelTests
{
    [Fact]
    public async Task Verification_SelectsPhaseByLevelAndFiltersPersistentTips()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");
        try
        {
            var mainInformation = new MatchupMainInformationViewModel(
                "ru-RU",
                TestGamePatchCatalog.Ready(),
                localization)
            {
                Author = "TopLaneAuthor"
            };
            var champions = new MatchupChampionsAndRoleViewModel(
                TestChampionCatalog.Ready(),
                localization);
            await champions.EnsureCatalogLoadedAsync();
            champions.SelectedPlayerChampion = champions.Champions.Single(
                static champion => champion.Id == "Urgot");
            champions.SelectedOpponentChampion = champions.Champions.Single(
                static champion => champion.Id == "Sett");
            var content = new MatchupContentViewModel(localization, TestItemCatalog.Ready());
            var earlyPhase = AddPhase(content, "Осторожное начало", 1, 8, "Не ищи размен первым");
            var latePhase = AddPhase(content, "Можно давить", 9, null, "Играй активнее");
            AddPersistentTip(content, "Весь матч", null);
            AddPersistentTip(content, "Только поздняя фаза", latePhase.Id);

            var verification = new MatchupVerificationViewModel(
                localization,
                new MatchupPhaseSelector());
            verification.Update(mainInformation, champions, content);

            Assert.Same(earlyPhase, verification.ActivePhase);
            Assert.Single(verification.VisiblePersistentTips);
            verification.SelectedLevel = 9;
            Assert.Same(latePhase, verification.ActivePhase);
            Assert.Equal(2, verification.VisiblePersistentTips.Count);
            Assert.Equal("фаза 2", verification.PhaseOrderLabel);
            Assert.True(verification.CanContinue);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    private static MatchupPhaseViewModel AddPhase(
        MatchupContentViewModel content,
        string name,
        int minimumLevel,
        int? maximumLevel,
        string advice)
    {
        content.BeginAddPhase();
        content.Editor.Name = name;
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

        content.Editor.PrimaryAdvice = advice;
        Assert.True(content.SaveEditor());
        return content.Phases[^1];
    }

    private static void AddPersistentTip(
        MatchupContentViewModel content,
        string title,
        string? phaseId)
    {
        content.PersistentTips.BeginAdd();
        content.PersistentTips.Editor.Title = title;
        content.PersistentTips.Editor.Text = "Проверяемая постоянная подсказка";
        content.PersistentTips.Editor.SelectedPhaseId = phaseId;
        Assert.True(content.PersistentTips.SaveEditor());
    }
}
