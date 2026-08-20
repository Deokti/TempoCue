using TempoCue.Application;

namespace TempoCue.App.Tests;

internal sealed class TestChampionSelectSource(ChampionSelectReadResult result)
    : IChampionSelectSource
{
    public Task<ChampionSelectReadResult> ReadAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(result);
}
