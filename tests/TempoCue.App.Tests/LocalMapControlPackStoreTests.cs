using System.IO;
using TempoCue.App.Persistence;
using TempoCue.Application;

namespace TempoCue.App.Tests;

public sealed class LocalMapControlPackStoreTests
{
    [Fact]
    public async Task SaveAndLoad_PersistPackAndSeparateState()
    {
        using var directory = new TestDirectory();
        var store = new LocalMapControlPackStore(directory.RootPath);
        var pack = TestMapControlPackFactory.Create();

        var result = await store.SaveAsync(pack, LocalCoachPackState.Enabled);
        var snapshot = await new LocalMapControlPackStore(directory.RootPath).LoadAsync();

        Assert.True(File.Exists(result.DisplayPath));
        var entry = Assert.Single(snapshot.Entries);
        Assert.Equal(pack.Id, entry.Pack.Id);
        Assert.Equal(LocalCoachPackState.Enabled, entry.State);
        Assert.Equal(LocalCoachPackOrigin.CreatedByUser, entry.Origin);
        Assert.True(File.Exists(Path.Combine(
            directory.RootPath,
            $"{pack.Id}.state.json")));
    }

    [Fact]
    public async Task Load_SkipsDamagedMapControlPack()
    {
        using var directory = new TestDirectory();
        var store = new LocalMapControlPackStore(directory.RootPath);
        await store.SaveAsync(
            TestMapControlPackFactory.Create(),
            LocalCoachPackState.Enabled);
        await File.WriteAllTextAsync(
            Path.Combine(directory.RootPath, "damaged.json"),
            "{ this is not a Map Control Pack }");

        var snapshot = await store.LoadAsync();

        Assert.Single(snapshot.Entries);
        Assert.Equal(1, snapshot.InvalidFileCount);
    }

    [Fact]
    public async Task Import_DoesNotOverwriteCreatedPack()
    {
        using var directory = new TestDirectory();
        var store = new LocalMapControlPackStore(directory.RootPath);
        var pack = TestMapControlPackFactory.Create();
        await store.SaveAsync(pack, LocalCoachPackState.Enabled);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.ImportAsync(
            pack with { Name = "Чужая версия" },
            LocalCoachPackState.Disabled));

        var entry = Assert.Single((await store.LoadAsync()).Entries);
        Assert.Equal(pack.Name, entry.Pack.Name);
        Assert.Equal(LocalCoachPackOrigin.CreatedByUser, entry.Origin);
    }

    [Fact]
    public async Task CollidingSafeFileName_DoesNotReplaceAnotherPack()
    {
        using var directory = new TestDirectory();
        var store = new LocalMapControlPackStore(directory.RootPath);
        var existing = TestMapControlPackFactory.Create("map-control:test");
        var collision = TestMapControlPackFactory.Create("map-control-test");
        await store.SaveAsync(existing, LocalCoachPackState.Enabled);

        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(
            collision,
            LocalCoachPackState.Enabled));

        Assert.Equal(existing.Id, Assert.Single((await store.LoadAsync()).Entries).Pack.Id);
    }

    [Fact]
    public async Task ConcurrentStateAndLoadOperations_KeepFilesReadable()
    {
        using var directory = new TestDirectory();
        var store = new LocalMapControlPackStore(directory.RootPath);
        var pack = TestMapControlPackFactory.Create();
        await store.SaveAsync(pack, LocalCoachPackState.Enabled);

        var operations = Enumerable.Range(0, 20)
            .Select(index => index % 2 == 0
                ? store.SetStateAsync(pack.Id, LocalCoachPackState.Enabled)
                : store.LoadAsync())
            .ToArray();
        await Task.WhenAll(operations);

        var snapshot = await store.LoadAsync();
        Assert.Equal(LocalCoachPackState.Enabled, Assert.Single(snapshot.Entries).State);
        Assert.Equal(0, snapshot.InvalidFileCount);
    }

    private sealed class TestDirectory : IDisposable
    {
        internal TestDirectory()
        {
            RootPath = Path.Combine(
                Path.GetTempPath(),
                "TempoCue.App.Tests",
                Guid.NewGuid().ToString("N"));
        }

        internal string RootPath { get; }

        public void Dispose()
        {
            if (Directory.Exists(RootPath))
            {
                Directory.Delete(RootPath, recursive: true);
            }
        }
    }
}
