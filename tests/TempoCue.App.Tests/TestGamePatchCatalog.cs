using TempoCue.Application;

namespace TempoCue.App.Tests;

internal sealed class TestGamePatchCatalog(GamePatchCatalogLoadResult result) : IGamePatchCatalog
{
    public static TestGamePatchCatalog Ready() => new(
        new GamePatchCatalogLoadResult(
            [
                new GamePatchCatalogEntry("26.15", IsCurrent: true),
                new GamePatchCatalogEntry("26.14", IsCurrent: false)
            ]));

    public Task<GamePatchCatalogLoadResult> LoadAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(result);
}
