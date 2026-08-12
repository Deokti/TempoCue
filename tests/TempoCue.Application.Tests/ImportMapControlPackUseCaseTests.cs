namespace TempoCue.Application.Tests;

public sealed class ImportMapControlPackUseCaseTests
{
    [Fact]
    public async Task InspectAndExecute_NewPack_ImportsItDisabled()
    {
        var store = new TestMapControlPackStore();
        var useCase = new ImportMapControlPackUseCase(store, []);
        var pack = TestMapControlPackFactory.Create("map-control.imported");
        await using var source = await CreatePortableStreamAsync(pack);

        var preview = await useCase.InspectAsync(source);
        var result = await useCase.ExecuteAsync(preview);

        Assert.Equal(MapControlPackImportConflict.None, preview.Conflict);
        Assert.True(result.Succeeded);
        var entry = Assert.Single((await store.LoadAsync()).Entries);
        Assert.Equal(LocalCoachPackState.Disabled, entry.State);
        Assert.Equal(LocalCoachPackOrigin.Imported, entry.Origin);
    }

    [Fact]
    public async Task Execute_ReplacesOnlyImportedPackAndPreservesState()
    {
        var store = new TestMapControlPackStore();
        var previous = TestMapControlPackFactory.Create("map-control.replace") with
        {
            Name = "Старая версия"
        };
        store.Add(previous, LocalCoachPackState.Enabled, LocalCoachPackOrigin.Imported);
        var useCase = new ImportMapControlPackUseCase(store, []);
        await using var source = await CreatePortableStreamAsync(previous with
        {
            Name = "Новая версия"
        });

        var preview = await useCase.InspectAsync(source);
        var result = await useCase.ExecuteAsync(preview);

        Assert.True(preview.RequiresReplacement);
        Assert.True(result.Succeeded);
        var entry = Assert.Single((await store.LoadAsync()).Entries);
        Assert.Equal("Новая версия", entry.Pack.Name);
        Assert.Equal(LocalCoachPackState.Enabled, entry.State);
    }

    [Theory]
    [InlineData(false, MapControlPackImportConflict.CreatedPack)]
    [InlineData(true, MapControlPackImportConflict.BuiltInPack)]
    public async Task Inspect_ProtectsCreatedAndBuiltInIdentifiers(
        bool isBuiltIn,
        MapControlPackImportConflict expectedConflict)
    {
        var pack = TestMapControlPackFactory.Create("map-control.protected");
        var store = new TestMapControlPackStore();
        if (!isBuiltIn)
        {
            store.Add(pack, LocalCoachPackState.Enabled, LocalCoachPackOrigin.CreatedByUser);
        }

        var useCase = new ImportMapControlPackUseCase(
            store,
            isBuiltIn ? [pack.Id] : []);
        await using var source = await CreatePortableStreamAsync(pack);

        var preview = await useCase.InspectAsync(source);

        Assert.Equal(expectedConflict, preview.Conflict);
        Assert.False(preview.CanImport);
    }

    private static async Task<MemoryStream> CreatePortableStreamAsync(MapControlPack pack)
    {
        var stream = new MemoryStream();
        await MapControlPackWriter.WriteAsync(stream, pack);
        stream.Position = 0;
        return stream;
    }
}
