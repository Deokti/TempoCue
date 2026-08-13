using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Проверяет навигацию библиотеки и локализацию установленного Coach Pack.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachingViewModelTests
{
    [Fact]
    public async Task CoachingNavigation_OpensMatchupEditor()
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
        Assert.True(viewModel.PackTypeSelection.IsGameControlAvailable);

        viewModel.OpenPackTypeSelection();
        Assert.Equal(CoachingScreen.PackTypeSelection, viewModel.CurrentScreen);

        viewModel.OpenMatchupEditor();
        Assert.Equal(CoachingScreen.MatchupEditor, viewModel.CurrentScreen);
        Assert.Equal(1, viewModel.MatchupEditor.CurrentStep);
        Assert.Equal(5, viewModel.MatchupEditor.TotalSteps);
        Assert.False(viewModel.MatchupEditor.MoveNext());

        await CompleteMainInformationAsync(viewModel.MatchupEditor);

        Assert.True(viewModel.MatchupEditor.MoveNext());
        Assert.Equal(2, viewModel.MatchupEditor.CurrentStep);
        Assert.False(viewModel.MatchupEditor.CanMoveNext);

        await SelectChampionsAsync(viewModel.MatchupEditor);

        Assert.True(viewModel.MatchupEditor.CanMoveNext);
        Assert.True(viewModel.MatchupEditor.MoveNext());
        Assert.Equal(3, viewModel.MatchupEditor.CurrentStep);
        Assert.False(viewModel.MatchupEditor.CanMoveNext);

        var content = AddPhase(viewModel.MatchupEditor);
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
    public async Task NewPack_AllowsVisitedValidStepsButDoesNotSkipUnvisitedOnes()
    {
        using var viewModel = CoachingViewModelTestFactory.Create(
            TestCoachPackFactory.CreateMatchup(),
            LocalizationService.Instance);
        viewModel.OpenMatchupEditor();

        Assert.False(viewModel.MatchupEditor.CanNavigateToStep3);
        Assert.False(viewModel.MatchupEditor.NavigateToStep(5));

        await CompleteMainInformationAsync(viewModel.MatchupEditor);
        Assert.True(viewModel.MatchupEditor.NavigateToStep(2));
        Assert.False(viewModel.MatchupEditor.CanNavigateToStep3);

        await SelectChampionsAsync(viewModel.MatchupEditor);
        Assert.True(viewModel.MatchupEditor.NavigateToStep(3));
        Assert.True(viewModel.MatchupEditor.NavigateToStep(1));
        Assert.True(viewModel.MatchupEditor.CanNavigateToStep3);

        viewModel.MatchupEditor.MainInformation.PackName = string.Empty;
        Assert.False(viewModel.MatchupEditor.CanNavigateToStep2);
        Assert.False(viewModel.MatchupEditor.NavigateToStep(3));
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

    private static async Task CompleteMainInformationAsync(MatchupEditorViewModel editor)
    {
        editor.MainInformation.PackName = "Urgot против Sett";
        editor.MainInformation.ShortDescription = "Как пережить раннюю линию и найти момент для боя.";
        editor.MainInformation.Author = "TopLaneAuthor";
        await editor.MainInformation.EnsureGamePatchCatalogLoadedAsync();
    }

    private static async Task SelectChampionsAsync(MatchupEditorViewModel editor)
    {
        await editor.ChampionsAndRole.EnsureCatalogLoadedAsync();
        editor.ChampionsAndRole.SelectedPlayerChampion = editor.ChampionsAndRole.Champions
            .Single(static champion => champion.Id == "Urgot");
        editor.ChampionsAndRole.SelectedOpponentChampion = editor.ChampionsAndRole.Champions
            .Single(static champion => champion.Id == "Sett");
    }

    private static MatchupContentViewModel AddPhase(MatchupEditorViewModel editor)
    {
        editor.Content.BeginAddPhase();
        editor.Content.Editor.Name = "Осторожное начало";
        editor.Content.Editor.PrimaryAdvice = "Не ищи размен первым";
        Assert.True(editor.Content.SaveEditor());
        return editor.Content;
    }

}
