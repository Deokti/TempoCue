using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;

namespace TempoCue.App.Tests;

/// <summary>Проверяет каталог и выбор сторон матчапа.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MatchupChampionsAndRoleViewModelTests
{
    [Fact]
    public async Task ChampionsAndRole_SelectsCatalogEntriesAndRejectsMirrorMatchup()
    {
        var viewModel = new MatchupChampionsAndRoleViewModel(
            TestChampionCatalog.Ready(),
            LocalizationService.Instance);

        Assert.Equal(MatchupLaneRole.Top, viewModel.SelectedRole);
        Assert.False(viewModel.IsComplete);
        Assert.Equal(CatalogViewState.NotLoaded, viewModel.CatalogState);

        await viewModel.EnsureCatalogLoadedAsync();
        Assert.Equal(CatalogViewState.Ready, viewModel.CatalogState);

        var urgot = Assert.Single(viewModel.Champions, champion => champion.Id == "Urgot");
        Assert.Equal("Urgot", urgot.DisplayName);

        var sett = Assert.Single(viewModel.Champions, champion => champion.Id == "Sett");
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
                        6,
                        "Urgot",
                        new Uri("https://example.test/Urgot.png"))
                ],
                ChampionCatalogSource.Cache));
        var cachedViewModel = new MatchupChampionsAndRoleViewModel(
            cachedCatalog,
            LocalizationService.Instance);

        await cachedViewModel.EnsureCatalogLoadedAsync();

        Assert.Equal(CatalogViewState.Cached, cachedViewModel.CatalogState);
        Assert.True(cachedViewModel.IsCatalogReady);
        Assert.True(cachedViewModel.IsUsingCache);

        var unavailableViewModel = new MatchupChampionsAndRoleViewModel(
            new TestChampionCatalog(ChampionCatalogLoadResult.Unavailable()),
            LocalizationService.Instance);

        await unavailableViewModel.EnsureCatalogLoadedAsync();

        Assert.Equal(CatalogViewState.Failed, unavailableViewModel.CatalogState);
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
        Assert.Same(viewModel.FilteredPickerChampions, viewModel.FilteredPickerChampions);

        viewModel.SelectPickerChampion(sett);

        Assert.False(viewModel.IsPickerOpen);
        Assert.Empty(viewModel.FilteredPickerChampions);
        Assert.Equal("Sett", viewModel.SelectedPlayerChampion?.Id);

        viewModel.OpenOpponentPicker();
        Assert.Equal(viewModel.Champions.Count, viewModel.FilteredPickerChampions.Count);
        var urgot = viewModel.FilteredPickerChampions.Single(champion => champion.Id == "Urgot");
        viewModel.SelectPickerChampion(urgot);

        Assert.Equal("Urgot", viewModel.SelectedOpponentChampion?.Id);
        Assert.True(viewModel.IsComplete);
    }

    [Fact]
    public async Task ChampionsAndRole_OlderCatalogRequestCannotReplaceNewLanguage()
    {
        var catalog = new SequencedChampionCatalog();
        var initialLanguage = LocalizationService.Instance.CurrentLanguageCode;
        var nextLanguage = initialLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? "en-US"
            : "ru-RU";
        var viewModel = new MatchupChampionsAndRoleViewModel(
            catalog,
            LocalizationService.Instance);
        var olderRequest = viewModel.EnsureCatalogLoadedAsync();

        viewModel.UpdateLanguage(nextLanguage);
        var currentRequest = viewModel.EnsureCatalogLoadedAsync();
        catalog.Complete(nextLanguage, CreateResult("Sett", "Sett"));
        await currentRequest;

        catalog.Complete(initialLanguage, CreateResult("Urgot", "Ургот"));
        await olderRequest;

        Assert.Equal(CatalogViewState.Ready, viewModel.CatalogState);
        Assert.Equal("Sett", Assert.Single(viewModel.Champions).Id);
    }

    private static ChampionCatalogLoadResult CreateResult(string id, string name) => new(
        [
            new ChampionCatalogEntry(
                id,
                1,
                name,
                new Uri($"https://example.test/{id}.png"))
        ],
        ChampionCatalogSource.Network);

    private sealed class SequencedChampionCatalog : IChampionCatalog
    {
        private readonly Dictionary<string, TaskCompletionSource<ChampionCatalogLoadResult>> _requests = [];

        public Task<ChampionCatalogLoadResult> LoadAsync(
            string languageCode,
            CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<ChampionCatalogLoadResult>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(languageCode, completion);
            return completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete(string languageCode, ChampionCatalogLoadResult result) =>
            _requests[languageCode].TrySetResult(result);
    }
}
