using TempoCue.Application;

namespace TempoCue.App.Tests;

internal sealed class TestLeagueClientRegionSource(string? region) : ILeagueClientRegionSource
{
    public static TestLeagueClientRegionSource EuropeWest() => new("EUW");

    public Task<string?> ReadRegionAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(region);
}
