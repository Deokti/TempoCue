using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Проверяет навигацию библиотеки и локализацию установленного Coach Pack.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachingViewModelTests
{
    [Fact]
    public async Task CoachingNavigation_OpensOnlyAvailablePackEditor()
    {
        var localization = LocalizationService.Instance;
        using var viewModel = new CoachingViewModel(
            new CoachPack
            {
                SchemaVersion = 1,
                Id = "test.navigation",
                Name = "Test",
                Patch = "test",
                Rules = []
            },
            localization,
            TestGamePatchCatalog.Ready(),
            TestChampionCatalog.Ready(),
            TestItemCatalog.Ready(),
            new SaveCoachPackUseCase(new TestCoachPackStore()));

        Assert.Equal(CoachingScreen.Library, viewModel.CurrentScreen);
        Assert.True(viewModel.PackTypeSelection.IsMatchupAvailable);
        Assert.False(viewModel.PackTypeSelection.IsGameControlAvailable);

        viewModel.OpenPackTypeSelection();
        Assert.Equal(CoachingScreen.PackTypeSelection, viewModel.CurrentScreen);

        viewModel.OpenMatchupEditor();
        Assert.Equal(CoachingScreen.MatchupEditor, viewModel.CurrentScreen);
        Assert.Equal(1, viewModel.MatchupEditor.CurrentStep);
        Assert.Equal(5, viewModel.MatchupEditor.TotalSteps);
        Assert.False(viewModel.MatchupEditor.MoveNext());

        var mainInformation = viewModel.MatchupEditor.MainInformation;
        mainInformation.PackName = "Urgot против Sett";
        mainInformation.ShortDescription = "Как пережить раннюю линию и найти момент для боя.";
        mainInformation.Author = "TopLaneAuthor";
        await mainInformation.EnsureGamePatchCatalogLoadedAsync();

        Assert.True(viewModel.MatchupEditor.MoveNext());
        Assert.Equal(2, viewModel.MatchupEditor.CurrentStep);
        Assert.False(viewModel.MatchupEditor.CanMoveNext);

        var championsAndRole = viewModel.MatchupEditor.ChampionsAndRole;
        await championsAndRole.EnsureCatalogLoadedAsync();
        championsAndRole.SelectedPlayerChampion = championsAndRole.Champions
            .Single(champion => champion.Id == "Urgot");
        championsAndRole.SelectedOpponentChampion = championsAndRole.Champions
            .Single(champion => champion.Id == "Sett");

        Assert.True(viewModel.MatchupEditor.CanMoveNext);
        Assert.True(viewModel.MatchupEditor.MoveNext());
        Assert.Equal(3, viewModel.MatchupEditor.CurrentStep);
        Assert.False(viewModel.MatchupEditor.CanMoveNext);

        var content = viewModel.MatchupEditor.Content;
        content.BeginAddPhase();
        content.Editor.Name = "Осторожное начало";
        content.Editor.PrimaryAdvice = "Не ищи размен первым";
        Assert.True(content.SaveEditor());
        Assert.Single(content.Phases);

        Assert.True(viewModel.MatchupEditor.MovePrevious());
        Assert.Equal(2, viewModel.MatchupEditor.CurrentStep);
        Assert.True(viewModel.MatchupEditor.MoveNext());
        Assert.Equal(3, viewModel.MatchupEditor.CurrentStep);
        Assert.Single(content.Phases);

        viewModel.OpenLibrary();
        Assert.Equal(CoachingScreen.Library, viewModel.CurrentScreen);
    }

    [Fact]
    public void LanguageChange_UpdatesSystemLabelsButPreservesPackContent()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var pack = new CoachPack
            {
                SchemaVersion = 1,
                Id = "test.urgot-vs-sett",
                Name = "Urgot vs Sett — авторский текст",
                Patch = "требует проверки",
                Rules =
                [
                    CreateRule(
                        "early",
                        1,
                        8,
                        PowerState.Dangerous,
                        "Не принимай ранний бой"),
                    CreateRule(
                        "late",
                        9,
                        null,
                        PowerState.Playable,
                        "Можно играть активнее")
                ]
            };
            using var viewModel = new CoachingViewModel(
                pack,
                localization,
                TestGamePatchCatalog.Ready(),
                TestChampionCatalog.Ready(),
                TestItemCatalog.Ready(),
                new SaveCoachPackUseCase(new TestCoachPackStore()));

            Assert.Equal("URG", viewModel.PlayerChampionInitials);
            Assert.Equal("SET", viewModel.OpponentChampionInitials);
            Assert.Equal("2 правил", viewModel.RulesSummary);
            Assert.Equal("Опасно", viewModel.Rules[0].PowerStateLabel);
            Assert.Equal("ур. 1–8", viewModel.Rules[0].LevelRange);
            Assert.Equal("ур. 9+", viewModel.Rules[1].LevelRange);

            localization.SetLanguage("en-US");

            Assert.Equal("2 rules", viewModel.RulesSummary);
            Assert.Equal("Danger", viewModel.Rules[0].PowerStateLabel);
            Assert.Equal("lvl. 1–8", viewModel.Rules[0].LevelRange);
            Assert.Equal("Не принимай ранний бой", viewModel.Rules[0].Title);
            Assert.Equal("Urgot vs Sett — авторский текст", viewModel.PackName);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

    private static CoachRuleDefinition CreateRule(
        string id,
        int minimumLevel,
        int? maximumLevel,
        PowerState powerState,
        string title) => new()
        {
            Id = id,
            PlayerChampion = "Urgot",
            OpponentChampion = "Sett",
            MinimumLevel = minimumLevel,
            MaximumLevel = maximumLevel,
            PowerState = powerState,
            Priority = 1,
            Title = title,
            Message = "Содержимое набора остаётся на языке автора.",
            Source = "Тестовый источник"
        };
}
