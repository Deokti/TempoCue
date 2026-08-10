using System.IO;
using System.Text.Json;
using TempoCue.App.Persistence;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

public sealed class LocalCoachPackStoreTests
{
    [Fact]
    public async Task SaveAsync_WritesPackAndSeparateLocalState()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "TempoCue.App.Tests",
            Guid.NewGuid().ToString("N"));
        try
        {
            var store = new LocalCoachPackStore(root);
            var pack = CreatePack();

            var result = await store.SaveAsync(pack, LocalCoachPackState.Enabled);

            Assert.True(File.Exists(result.DisplayPath));
            Assert.Equal(pack.Id, CoachPackLoader.LoadFromFile(result.DisplayPath).Id);
            var statePath = Path.Combine(root, $"{pack.Id}.state.json");
            Assert.True(File.Exists(statePath));
            using var state = JsonDocument.Parse(await File.ReadAllTextAsync(statePath));
            Assert.Equal("enabled", state.RootElement.GetProperty("state").GetString());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static CoachPack CreatePack() => new()
    {
        SchemaVersion = 2,
        Id = "matchup.urgot-vs-sett.store-test",
        Name = "Urgot против Sett",
        Patch = "26.15",
        Description = "Тест локального хранилища Coach Pack.",
        Author = "TestAuthor",
        ContentLanguage = "ru-RU",
        PlayerChampion = "Urgot",
        OpponentChampion = "Sett",
        LaneRole = "top",
        VerifiedOn = new DateOnly(2026, 8, 10),
        Source = new CoachPackSource
        {
            Kind = CoachPackSourceKind.PersonalExperience,
            Permission = CoachPackSourcePermission.NotRequired
        },
        Rules =
        [
            new CoachRuleDefinition
            {
                Id = "phase-1",
                PlayerChampion = "Urgot",
                OpponentChampion = "Sett",
                PowerState = PowerState.Cautious,
                Priority = 1,
                Title = "Начало",
                Message = "Играй аккуратно",
                Source = "Личный опыт автора"
            }
        ]
    };
}
