using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;

namespace TempoCue.App.Tests;

/// <summary>Проверяет различие между редактированием собственного набора и созданием копии.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class CoachPackEditingViewModelTests
{
    [Fact]
    public async Task OwnPack_LoadsIntoEditorAndKeepsIdentityAndState()
    {
        var localization = LocalizationService.Instance;
        localization.SetLanguage("ru-RU");
        var store = new TestCoachPackStore();
        var pack = TestCoachPackFactory.CreateDetailedMatchup();
        await store.SaveAsync(pack, LocalCoachPackState.Disabled);
        using var coaching = CoachingViewModelTestFactory.Create(
            TestCoachPackFactory.CreateMatchup("built-in.urgot-vs-sett"),
            localization,
            store);
        await coaching.Library.RefreshAsync();
        var card = Assert.IsType<CoachingPackCardViewModel>(coaching.Library.FindCard(pack.Id));

        await coaching.OpenPackDetailsAsync(card);
        await coaching.OpenSelectedPackEditorAsync();

        var editor = coaching.MatchupEditor;
        Assert.Equal(MatchupEditorMode.Edit, editor.Mode);
        Assert.Equal(pack.Name, editor.MainInformation.PackName);
        Assert.Equal(pack.Author, editor.MainInformation.Author);
        Assert.Equal(2, editor.Content.Phases.Count);
        Assert.Equal(3071, Assert.Single(editor.Content.Phases[1].RecommendedItems).Id);
        Assert.Single(editor.Content.PersistentTips.Tips);
        Assert.Equal(2, editor.Content.ItemBlocks.Blocks.Count);
        Assert.True(editor.CanNavigateToStep5);
        Assert.True(editor.NavigateToStep(5));
        Assert.Equal(5, editor.CurrentStep);
        Assert.True(editor.NavigateToStep(2));
        Assert.Equal(2, editor.CurrentStep);

        editor.MainInformation.PackName = "Обновлённый матчап";
        editor.Save.Update(editor.MainInformation, editor.ChampionsAndRole, editor.Content);
        Assert.True(await editor.Save.SaveAsync(editor.Save.OriginalState));
        Assert.Equal(pack.Id, store.SavedPack?.Id);
        Assert.Equal("Обновлённый матчап", store.SavedPack?.Name);
        Assert.Equal(LocalCoachPackState.Disabled, store.SavedState);
    }

    [Fact]
    public async Task ForeignPack_CreatesIndependentAttributedCopy()
    {
        var localization = LocalizationService.Instance;
        localization.SetLanguage("ru-RU");
        var store = new TestCoachPackStore();
        var original = TestCoachPackFactory.CreateDetailedMatchup();
        using var coaching = CoachingViewModelTestFactory.Create(original, localization, store);
        await coaching.Library.RefreshAsync();
        var card = Assert.IsType<CoachingPackCardViewModel>(coaching.Library.FindCard(original.Id));
        Assert.Equal(CoachingPackOrigin.BuiltIn, card.Origin);

        await coaching.OpenPackDetailsAsync(card);
        await coaching.OpenSelectedPackEditorAsync();

        var editor = coaching.MatchupEditor;
        Assert.Equal(MatchupEditorMode.Copy, editor.Mode);
        Assert.Contains("моя копия", editor.MainInformation.PackName);
        Assert.Empty(editor.MainInformation.Author);

        editor.MainInformation.Author = "LocalAuthor";
        editor.Save.Update(editor.MainInformation, editor.ChampionsAndRole, editor.Content);
        Assert.True(await editor.Save.SaveAsync(LocalCoachPackState.Draft));
        Assert.NotNull(store.SavedPack);
        Assert.NotEqual(original.Id, store.SavedPack.Id);
        Assert.Equal(original.Id, store.SavedPack.ForkedFromPackId);
        Assert.Equal(original.Author, store.SavedPack.ForkedFromAuthor);
        Assert.Equal(LocalCoachPackState.Draft, store.SavedState);
        Assert.Equal("Urgot против Sett", original.Name);
    }
}
