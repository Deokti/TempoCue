using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
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
        using var viewModel = CoachingViewModelTestFactory.Create(
            TestCoachPackFactory.CreateMatchup(
                id: "test.navigation",
                name: "Test",
                rules: []),
            localization);

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
    public async Task LanguageChange_UpdatesLibraryLabelsButPreservesPackContent()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        localization.SetLanguage("ru-RU");

        try
        {
            var pack = TestCoachPackFactory.CreateMatchup(
                name: "Urgot vs Sett — авторский текст",
                patch: "требует проверки",
                rules:
                [
                    TestCoachPackFactory.CreateRule(
                        "early",
                        1,
                        8,
                        PowerState.Dangerous,
                        "Не принимай ранний бой"),
                    TestCoachPackFactory.CreateRule(
                        "late",
                        9,
                        null,
                        PowerState.Playable,
                        "Можно играть активнее")
                ]);
            using var viewModel = CoachingViewModelTestFactory.Create(
                pack,
                localization);
            await viewModel.Library.EnsureLoadedAsync();

            var russianCard = Assert.Single(viewModel.Library.MatchupCards);
            Assert.Equal("URG", russianCard.PlayerChampionInitials);
            Assert.Equal("SET", russianCard.OpponentChampionInitials);
            Assert.Equal("Urgot против Sett", russianCard.Title);
            Assert.Equal("2 совета", russianCard.AdviceCount);
            Assert.Equal("Включён", russianCard.StateLabel);

            localization.SetLanguage("en-US");

            var englishCard = Assert.Single(viewModel.Library.MatchupCards);
            Assert.Equal("Urgot vs Sett", englishCard.Title);
            Assert.Equal("2 pieces of advice", englishCard.AdviceCount);
            Assert.Equal("Enabled", englishCard.StateLabel);
            Assert.Equal("Urgot vs Sett — авторский текст", englishCard.Pack.Name);
            Assert.Equal("Не принимай ранний бой", englishCard.Pack.Rules[0].Title);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
        }
    }

}
