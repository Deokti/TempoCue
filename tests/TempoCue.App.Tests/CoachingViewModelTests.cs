using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Проверяет подготовку Coach Pack для библиотеки коучинга.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachingViewModelTests
{
    [Fact]
    public void MainInformation_RequiresMetadataAndAValidLinkedSource()
    {
        var viewModel = new MatchupMainInformationViewModel("ru-RU");

        Assert.Equal("ru-RU", viewModel.AdviceLanguageCode);
        Assert.Equal(MatchupSourceKind.PersonalExperience, viewModel.SelectedSourceKind);
        Assert.True(viewModel.IsSourceReady);
        Assert.False(viewModel.IsComplete);

        viewModel.PackName = "Urgot против Sett";
        viewModel.ShortDescription = "Как пережить раннюю линию и найти момент для боя.";
        viewModel.Author = "TopLaneAuthor";
        viewModel.GamePatch = "26.15";

        Assert.True(viewModel.IsComplete);

        viewModel.SelectedSourceKind = MatchupSourceKind.Article;
        Assert.True(viewModel.IsSourceUrlRequired);
        Assert.False(viewModel.IsSourceReady);
        Assert.False(viewModel.IsComplete);

        viewModel.SourceUrl = "not-a-web-link";
        Assert.False(viewModel.IsSourceReady);

        viewModel.SourceUrl = "https://example.com/urgot-sett";
        Assert.True(viewModel.IsSourceReady);
        Assert.True(viewModel.IsComplete);
    }

    [Fact]
    public void CoachingNavigation_OpensOnlyAvailablePackEditor()
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
            localization);

        Assert.Equal(CoachingScreen.Library, viewModel.CurrentScreen);
        Assert.True(viewModel.PackTypeSelection.IsMatchupAvailable);
        Assert.False(viewModel.PackTypeSelection.IsGameControlAvailable);

        viewModel.OpenPackTypeSelection();
        Assert.Equal(CoachingScreen.PackTypeSelection, viewModel.CurrentScreen);

        viewModel.OpenMatchupEditor();
        Assert.Equal(CoachingScreen.MatchupEditor, viewModel.CurrentScreen);
        Assert.Equal(1, viewModel.MatchupEditor.CurrentStep);
        Assert.Equal(7, viewModel.MatchupEditor.TotalSteps);

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
            using var viewModel = new CoachingViewModel(pack, localization);

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
