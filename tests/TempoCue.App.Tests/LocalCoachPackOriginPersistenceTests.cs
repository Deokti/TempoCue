using System.IO;
using System.Text.Json;
using TempoCue.App.Persistence;
using TempoCue.Application;

namespace TempoCue.App.Tests;

/// <summary>Проверяет локальную границу владения независимо от будущего сервера библиотеки.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class LocalCoachPackOriginPersistenceTests
{
    [Fact]
    public async Task ImportedOrigin_SurvivesStateChangeAndForkMetadataRoundTrip()
    {
        using var directory = new TemporaryDirectory();
        var store = new LocalCoachPackStore(directory.Path);
        var original = TestCoachPackFactory.CreateDetailedMatchup();
        var fork = original with
        {
            Id = "test.urgot-vs-sett.fork",
            ForkedFromPackId = original.Id,
            ForkedFromAuthor = original.Author
        };
        await store.ImportAsync(fork, LocalCoachPackState.Enabled);

        await store.SetStateAsync(fork.Id, LocalCoachPackState.Disabled);
        var entry = Assert.Single((await store.LoadAsync()).Entries);

        Assert.Equal(LocalCoachPackOrigin.Imported, entry.Origin);
        Assert.Equal(LocalCoachPackState.Disabled, entry.State);
        Assert.Equal(original.Id, entry.Pack.ForkedFromPackId);
        Assert.Equal(original.Author, entry.Pack.ForkedFromAuthor);
    }

    [Fact]
    public async Task VersionOneState_IsTreatedAsUserCreatedForCompatibility()
    {
        using var directory = new TemporaryDirectory();
        var store = new LocalCoachPackStore(directory.Path);
        var pack = TestCoachPackFactory.CreateDetailedMatchup();
        await store.SaveAsync(pack, LocalCoachPackState.Enabled);
        var statePath = System.IO.Path.Combine(directory.Path, $"{pack.Id}.state.json");
        await File.WriteAllTextAsync(
            statePath,
            JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                packId = pack.Id,
                state = "enabled",
                savedAtUtc = DateTimeOffset.UtcNow
            }));

        var entry = Assert.Single((await store.LoadAsync()).Entries);

        Assert.Equal(LocalCoachPackOrigin.CreatedByUser, entry.Origin);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"TempoCue.Tests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
