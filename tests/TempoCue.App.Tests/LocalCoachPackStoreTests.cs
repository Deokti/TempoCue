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
        using var directory = new TestCoachPackDirectory();
        var store = new LocalCoachPackStore(directory.RootPath);
        var pack = CreatePack();

        var result = await store.SaveAsync(pack, LocalCoachPackState.Enabled);

        Assert.True(File.Exists(result.DisplayPath));
        await using var savedPackStream = File.OpenRead(result.DisplayPath);
        Assert.Equal(pack.Id, CoachPackLoader.Load(savedPackStream).Id);
        var statePath = Path.Combine(directory.RootPath, $"{pack.Id}.state.json");
        Assert.True(File.Exists(statePath));
        using var state = JsonDocument.Parse(await File.ReadAllTextAsync(statePath));
        Assert.Equal("enabled", state.RootElement.GetProperty("state").GetString());
    }

    [Fact]
    public async Task LoadAsync_ReturnsSavedPackAndSkipsDamagedFile()
    {
        using var directory = new TestCoachPackDirectory();
        var store = new LocalCoachPackStore(directory.RootPath);
        var pack = CreatePack();
        await store.SaveAsync(pack, LocalCoachPackState.Enabled);
        await File.WriteAllTextAsync(
            Path.Combine(directory.RootPath, "damaged.json"),
            "{ this is not a Coach Pack }");

        var snapshot = await store.LoadAsync();

        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(pack.Id, entry.Pack.Id);
        Assert.Equal(LocalCoachPackState.Enabled, entry.State);
        Assert.Equal(1, snapshot.InvalidFileCount);
    }

    [Fact]
    public async Task SetStateAsync_PersistsDisabledStateWithoutPackFile()
    {
        using var directory = new TestCoachPackDirectory();
        var store = new LocalCoachPackStore(directory.RootPath);

        await store.SetStateAsync(
            "built-in.urgot-vs-sett",
            LocalCoachPackState.Disabled);
        var snapshot = await new LocalCoachPackStore(directory.RootPath).LoadAsync();

        Assert.Empty(snapshot.Entries);
        Assert.Equal(
            LocalCoachPackState.Disabled,
            snapshot.PackStates["built-in.urgot-vs-sett"]);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPackAndItsLocalState()
    {
        using var directory = new TestCoachPackDirectory();
        var store = new LocalCoachPackStore(directory.RootPath);
        var pack = CreatePack();
        var saved = await store.SaveAsync(pack, LocalCoachPackState.Enabled);
        var statePath = Path.Combine(directory.RootPath, $"{pack.Id}.state.json");

        var deleted = await store.DeleteAsync(pack.Id);

        Assert.True(deleted);
        Assert.False(File.Exists(saved.DisplayPath));
        Assert.False(File.Exists(statePath));
        Assert.Empty((await store.LoadAsync()).Entries);
        Assert.False(await store.DeleteAsync(pack.Id));
    }

    [Fact]
    public async Task ConcurrentOperations_KeepPackAndStateFilesReadable()
    {
        using var directory = new TestCoachPackDirectory();
        var store = new LocalCoachPackStore(directory.RootPath);
        var pack = CreatePack();
        await store.SaveAsync(pack, LocalCoachPackState.Enabled);

        var operations = Enumerable.Range(0, 20)
            .Select(index => index % 2 == 0
                ? store.SetStateAsync(pack.Id, LocalCoachPackState.Enabled)
                : store.LoadAsync())
            .ToArray();

        await Task.WhenAll(operations);
        var snapshot = await store.LoadAsync();

        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(pack.Id, entry.Pack.Id);
        Assert.Equal(LocalCoachPackState.Enabled, entry.State);
        Assert.Equal(0, snapshot.InvalidFileCount);
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

    private sealed class TestCoachPackDirectory : IDisposable
    {
        public TestCoachPackDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "TempoCue.App.Tests",
                Guid.NewGuid().ToString("N"));
        }

        public string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
