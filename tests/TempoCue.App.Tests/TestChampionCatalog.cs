using TempoCue.Application;

namespace TempoCue.App.Tests;

internal sealed class TestChampionCatalog(ChampionCatalogLoadResult result) : IChampionCatalog
{
    public static TestChampionCatalog Ready() => new(
        new ChampionCatalogLoadResult(
            [
                new ChampionCatalogEntry(
                    "Urgot",
                    6,
                    "Urgot",
                    new Uri("https://example.test/Urgot.png")),
                new ChampionCatalogEntry(
                    "Sett",
                    875,
                    "Sett",
                    new Uri("https://example.test/Sett.png")),
                new ChampionCatalogEntry(
                    "Aatrox",
                    266,
                    "Aatrox",
                    new Uri("https://example.test/Aatrox.png"))
            ],
            ChampionCatalogSource.Network));

    public Task<ChampionCatalogLoadResult> LoadAsync(
        string languageCode,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(result);
}

internal sealed class DeferredChampionCatalog : IChampionCatalog
{
    private readonly TaskCompletionSource<ChampionCatalogLoadResult> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ChampionCatalogLoadResult> LoadAsync(
        string languageCode,
        CancellationToken cancellationToken = default) =>
        _completion.Task.WaitAsync(cancellationToken);

    public void Complete(ChampionCatalogLoadResult result) =>
        _completion.TrySetResult(result);
}
