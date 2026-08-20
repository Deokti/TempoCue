using System.Text.Json;

namespace TempoCue.Riot.Tests;

public sealed class LeagueClientRegionSourceTests
{
    [Fact]
    public async Task ReadRegionAsync_ReadsRegionFromRegisteredLeagueInstallation()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var installDirectory = Path.Combine(root, "League of Legends");
            var configDirectory = Path.Combine(installDirectory, "Config");
            Directory.CreateDirectory(configDirectory);
            await File.WriteAllTextAsync(
                Path.Combine(configDirectory, "LeagueClientSettings.yaml"),
                "install:\n    globals:\n        locale: \"ru_RU\"\n        region: \"EUW\"\n");
            var manifestPath = Path.Combine(root, "RiotClientInstalls.json");
            await File.WriteAllTextAsync(
                manifestPath,
                JsonSerializer.Serialize(new
                {
                    associated_client = new Dictionary<string, string>
                    {
                        [installDirectory.Replace('\\', '/') + "/"] = "RiotClientServices.exe"
                    }
                }));
            var source = new LeagueClientRegionSource(manifestPath);

            var region = await source.ReadRegionAsync();

            Assert.Equal("EUW", region);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadRegionAsync_ReturnsNullForUnknownInstallation()
    {
        var source = new LeagueClientRegionSource(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.json"));

        var region = await source.ReadRegionAsync();

        Assert.Null(region);
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "TempoCue.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
