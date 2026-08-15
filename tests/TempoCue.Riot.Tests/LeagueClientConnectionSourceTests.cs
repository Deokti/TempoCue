using System.Text.Json;

namespace TempoCue.Riot.Tests;

public sealed class LeagueClientConnectionSourceTests
{
    [Fact]
    public async Task ReadAsync_ReadsLoopbackConnectionFromInstallationLockfile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var leagueDirectory = Path.Combine(root, "League of Legends");
        Directory.CreateDirectory(leagueDirectory);
        var installationsPath = Path.Combine(root, "RiotClientInstalls.json");
        await File.WriteAllTextAsync(
            installationsPath,
            JsonSerializer.Serialize(new
            {
                associated_client = new Dictionary<string, string>
                {
                    [Path.Combine(leagueDirectory, "LeagueClient.exe")] = "live"
                }
            }));
        await File.WriteAllTextAsync(
            Path.Combine(leagueDirectory, "lockfile"),
            "LeagueClient:1234:45678:test-password:https");

        try
        {
            var source = new LeagueClientConnectionSource(installationsPath);

            var connection = await source.ReadAsync();

            Assert.NotNull(connection);
            Assert.Equal("https://127.0.0.1:45678/", connection.BaseAddress.AbsoluteUri);
            Assert.Equal("test-password", connection.Password);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsNonHttpsLockfile()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var installationsPath = Path.Combine(root, "RiotClientInstalls.json");
        await File.WriteAllTextAsync(
            installationsPath,
            JsonSerializer.Serialize(new
            {
                associated_client = new Dictionary<string, string>
                {
                    [root] = "live"
                }
            }));
        await File.WriteAllTextAsync(
            Path.Combine(root, "lockfile"),
            "LeagueClient:1234:45678:test-password:http");

        try
        {
            var source = new LeagueClientConnectionSource(installationsPath);

            Assert.Null(await source.ReadAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
