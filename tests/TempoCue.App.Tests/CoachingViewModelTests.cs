using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
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
            localization,
            TestChampionCatalog.Ready());

        Assert.Equal(CoachingScreen.Library, viewModel.CurrentScreen);
        Assert.True(viewModel.PackTypeSelection.IsMatchupAvailable);
        Assert.False(viewModel.PackTypeSelection.IsGameControlAvailable);

        viewModel.OpenPackTypeSelection();
        Assert.Equal(CoachingScreen.PackTypeSelection, viewModel.CurrentScreen);

        viewModel.OpenMatchupEditor();
        Assert.Equal(CoachingScreen.MatchupEditor, viewModel.CurrentScreen);
        Assert.Equal(1, viewModel.MatchupEditor.CurrentStep);
        Assert.Equal(7, viewModel.MatchupEditor.TotalSteps);
        Assert.False(viewModel.MatchupEditor.MoveNext());

        var mainInformation = viewModel.MatchupEditor.MainInformation;
        mainInformation.PackName = "Urgot против Sett";
        mainInformation.ShortDescription = "Как пережить раннюю линию и найти момент для боя.";
        mainInformation.Author = "TopLaneAuthor";
        mainInformation.GamePatch = "26.15";

        Assert.True(viewModel.MatchupEditor.MoveNext());
        Assert.Equal(2, viewModel.MatchupEditor.CurrentStep);
        Assert.False(viewModel.MatchupEditor.CanMoveNext);
        Assert.True(viewModel.MatchupEditor.MovePrevious());
        Assert.Equal(1, viewModel.MatchupEditor.CurrentStep);

        viewModel.OpenLibrary();
        Assert.Equal(CoachingScreen.Library, viewModel.CurrentScreen);
    }

    [Fact]
    public async Task ChampionsAndRole_FiltersCatalogAndRejectsMirrorMatchup()
    {
        var viewModel = new MatchupChampionsAndRoleViewModel(
            TestChampionCatalog.Ready(),
            LocalizationService.Instance);

        Assert.Equal(MatchupLaneRole.Top, viewModel.SelectedRole);
        Assert.False(viewModel.IsComplete);
        Assert.Equal(ChampionCatalogViewState.NotLoaded, viewModel.CatalogState);

        await viewModel.EnsureCatalogLoadedAsync();
        Assert.Equal(ChampionCatalogViewState.Ready, viewModel.CatalogState);

        viewModel.PlayerSearch = "urg";
        var urgot = Assert.Single(viewModel.PlayerSuggestions);
        Assert.Equal("Urgot", urgot.DisplayName);

        viewModel.OpponentSearch = "set";
        var sett = Assert.Single(viewModel.OpponentSuggestions);
        Assert.Equal("Sett", sett.DisplayName);

        viewModel.SelectedPlayerChampion = urgot;
        viewModel.SelectedOpponentChampion = urgot;
        Assert.True(viewModel.HasSameChampion);
        Assert.False(viewModel.IsComplete);

        viewModel.SelectedOpponentChampion = sett;
        Assert.False(viewModel.HasSameChampion);
        Assert.True(viewModel.IsComplete);
    }

    [Fact]
    public async Task ChampionsAndRole_DistinguishesCachedCatalogAndLoadFailure()
    {
        var cachedCatalog = new TestChampionCatalog(
            new ChampionCatalogLoadResult(
                [
                    new ChampionCatalogEntry(
                        "Urgot",
                        "Urgot",
                        new Uri("https://example.test/Urgot.png"))
                ],
                ChampionCatalogSource.Cache));
        var cachedViewModel = new MatchupChampionsAndRoleViewModel(
            cachedCatalog,
            LocalizationService.Instance);

        await cachedViewModel.EnsureCatalogLoadedAsync();

        Assert.Equal(ChampionCatalogViewState.Cached, cachedViewModel.CatalogState);
        Assert.True(cachedViewModel.IsCatalogReady);
        Assert.True(cachedViewModel.IsUsingCache);

        var unavailableViewModel = new MatchupChampionsAndRoleViewModel(
            new TestChampionCatalog(ChampionCatalogLoadResult.Unavailable()),
            LocalizationService.Instance);

        await unavailableViewModel.EnsureCatalogLoadedAsync();

        Assert.Equal(ChampionCatalogViewState.Failed, unavailableViewModel.CatalogState);
        Assert.True(unavailableViewModel.HasLoadError);
        Assert.False(unavailableViewModel.IsCatalogReady);
    }

    [Fact]
    public async Task ChampionsAndRole_PickerSearchesAndUpdatesRequestedSide()
    {
        var viewModel = new MatchupChampionsAndRoleViewModel(
            TestChampionCatalog.Ready(),
            LocalizationService.Instance);
        await viewModel.EnsureCatalogLoadedAsync();

        viewModel.OpenPlayerPicker();

        Assert.True(viewModel.IsPickerOpen);
        Assert.Equal(ChampionPickerTarget.Player, viewModel.PickerTarget);
        viewModel.PickerSearch = "set";
        var sett = Assert.Single(viewModel.FilteredPickerChampions);

        viewModel.SelectPickerChampion(sett);

        Assert.False(viewModel.IsPickerOpen);
        Assert.Equal("Sett", viewModel.SelectedPlayerChampion?.Id);

        viewModel.OpenOpponentPicker();
        var urgot = viewModel.FilteredPickerChampions.Single(champion => champion.Id == "Urgot");
        viewModel.SelectPickerChampion(urgot);

        Assert.Equal("Urgot", viewModel.SelectedOpponentChampion?.Id);
        Assert.True(viewModel.IsComplete);
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
                TestChampionCatalog.Ready());

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
