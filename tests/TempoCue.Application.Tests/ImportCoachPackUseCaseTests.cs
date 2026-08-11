using System.Text;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class ImportCoachPackUseCaseTests
{
    [Fact]
    public async Task InspectAndExecute_NewPack_ImportsItDisabled()
    {
        var store = new InMemoryCoachPackStore();
        var useCase = new ImportCoachPackUseCase(store, []);
        var pack = CreatePack("matchup.new-import");
        await using var source = await CreatePortableStreamAsync(pack);

        var preview = await useCase.InspectAsync(source);
        var result = await useCase.ExecuteAsync(preview);

        Assert.Equal(CoachPackImportConflict.None, preview.Conflict);
        Assert.True(preview.CanImport);
        Assert.True(result.Succeeded);
        var entry = Assert.Single((await store.LoadAsync()).Entries);
        Assert.Equal(pack.Id, entry.Pack.Id);
        Assert.Equal(LocalCoachPackOrigin.Imported, entry.Origin);
        Assert.Equal(LocalCoachPackState.Disabled, entry.State);
    }

    [Fact]
    public async Task ExecuteAsync_ReplacesImportedPackAndPreservesItsState()
    {
        var store = new InMemoryCoachPackStore();
        var previous = CreatePack("matchup.imported") with { Name = "Старая версия" };
        store.Add(previous, LocalCoachPackState.Enabled, LocalCoachPackOrigin.Imported);
        var updated = previous with { Name = "Новая версия" };
        var useCase = new ImportCoachPackUseCase(store, []);
        await using var source = await CreatePortableStreamAsync(updated);

        var preview = await useCase.InspectAsync(source);
        var result = await useCase.ExecuteAsync(preview);

        Assert.True(preview.RequiresReplacement);
        Assert.True(result.Succeeded);
        var entry = Assert.Single((await store.LoadAsync()).Entries);
        Assert.Equal("Новая версия", entry.Pack.Name);
        Assert.Equal(LocalCoachPackState.Enabled, entry.State);
        Assert.Equal(LocalCoachPackOrigin.Imported, entry.Origin);
    }

    [Theory]
    [InlineData(LocalCoachPackOrigin.CreatedByUser, CoachPackImportConflict.CreatedPack)]
    [InlineData(LocalCoachPackOrigin.Imported, CoachPackImportConflict.BuiltInPack)]
    public async Task InspectAsync_ProtectsOwnedAndBuiltInIdentifiers(
        LocalCoachPackOrigin existingOrigin,
        CoachPackImportConflict expectedConflict)
    {
        var pack = CreatePack("matchup.protected");
        var store = new InMemoryCoachPackStore();
        var builtInIds = expectedConflict == CoachPackImportConflict.BuiltInPack
            ? new[] { pack.Id }
            : [];
        if (expectedConflict == CoachPackImportConflict.CreatedPack)
        {
            store.Add(pack, LocalCoachPackState.Enabled, existingOrigin);
        }

        var useCase = new ImportCoachPackUseCase(store, builtInIds);
        await using var source = await CreatePortableStreamAsync(pack);

        var preview = await useCase.InspectAsync(source);
        var result = await useCase.ExecuteAsync(preview);

        Assert.Equal(expectedConflict, preview.Conflict);
        Assert.False(preview.CanImport);
        Assert.False(result.Succeeded);
        Assert.Null(result.SaveResult);
    }

    [Fact]
    public async Task ExecuteAsync_RequiresConfirmationForConflictCreatedAfterPreview()
    {
        var pack = CreatePack("matchup.concurrent-import");
        var store = new InMemoryCoachPackStore();
        var useCase = new ImportCoachPackUseCase(store, []);
        await using var source = await CreatePortableStreamAsync(pack);
        var preview = await useCase.InspectAsync(source);
        store.Add(pack, LocalCoachPackState.Disabled, LocalCoachPackOrigin.Imported);

        var result = await useCase.ExecuteAsync(preview);

        Assert.False(result.Succeeded);
        Assert.True(result.Preview.RequiresReplacement);
    }

    [Fact]
    public async Task InspectAsync_RejectsMalformedJson()
    {
        var useCase = new ImportCoachPackUseCase(new InMemoryCoachPackStore(), []);
        await using var source = new MemoryStream(Encoding.UTF8.GetBytes("{ damaged json"));

        await Assert.ThrowsAsync<System.Text.Json.JsonException>(
            () => useCase.InspectAsync(source));
    }

    private static CoachPack CreatePack(string id) => new()
    {
        SchemaVersion = 2,
        Id = id,
        Name = "Urgot против Sett",
        Patch = "26.15",
        Description = "Переносимый тестовый Coach Pack.",
        Author = "TestAuthor",
        ContentLanguage = "ru-RU",
        PlayerChampion = "Urgot",
        OpponentChampion = "Sett",
        LaneRole = "top",
        VerifiedOn = new DateOnly(2026, 8, 11),
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
                MinimumLevel = 1,
                MaximumLevel = 18,
                PowerState = PowerState.Cautious,
                Priority = 1000,
                Title = "Основная фаза",
                Message = "Играй от безопасной дистанции.",
                Source = "Личный опыт"
            }
        ]
    };

    private static async Task<MemoryStream> CreatePortableStreamAsync(CoachPack pack)
    {
        var stream = new MemoryStream();
        await CoachPackWriter.WriteAsync(stream, pack);
        stream.Position = 0;
        return stream;
    }

    private sealed class InMemoryCoachPackStore : ICoachPackStore
    {
        private readonly List<LocalCoachPackEntry> _entries = [];

        public void Add(
            CoachPack pack,
            LocalCoachPackState state,
            LocalCoachPackOrigin origin)
        {
            _entries.RemoveAll(entry => string.Equals(
                entry.Pack.Id,
                pack.Id,
                StringComparison.OrdinalIgnoreCase));
            _entries.Add(new LocalCoachPackEntry(pack, state, DateTimeOffset.UtcNow, origin));
        }

        public Task<CoachPackLibrarySnapshot> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CoachPackLibrarySnapshot(
                _entries.ToArray(),
                0,
                _entries.ToDictionary(
                    static entry => entry.Pack.Id,
                    static entry => entry.State,
                    StringComparer.OrdinalIgnoreCase)));
        }

        public Task<CoachPackSaveResult> SaveAsync(
            CoachPack pack,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CoachPackSaveResult> ImportAsync(
            CoachPack pack,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Add(pack, state, LocalCoachPackOrigin.Imported);
            return Task.FromResult(new CoachPackSaveResult(pack.Id, state, pack.Id));
        }

        public Task SetStateAsync(
            string packId,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            string packId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
