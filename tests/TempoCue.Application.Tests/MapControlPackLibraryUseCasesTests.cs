namespace TempoCue.Application.Tests;

public sealed class MapControlPackLibraryUseCasesTests
{
    [Fact]
    public async Task SaveLoadSetStateAndDelete_UseOneStoreBoundary()
    {
        var store = new TestMapControlPackStore();
        var pack = TestMapControlPackFactory.Create();
        var save = new SaveMapControlPackUseCase(store);
        var load = new LoadMapControlPackLibraryUseCase(store);
        var setState = new SetMapControlPackStateUseCase(store);
        var delete = new DeleteMapControlPackUseCase(store);

        await save.ExecuteAsync(pack, LocalCoachPackState.Draft);
        await setState.ExecuteAsync(pack.Id, LocalCoachPackState.Enabled);
        var enabledSnapshot = await load.ExecuteAsync();
        var deleted = await delete.ExecuteAsync(pack.Id);

        Assert.Equal(LocalCoachPackState.Enabled, Assert.Single(enabledSnapshot.Entries).State);
        Assert.True(deleted);
        Assert.Empty((await load.ExecuteAsync()).Entries);
    }

    [Fact]
    public async Task SetState_RejectsDraftFromLibraryToggle()
    {
        var useCase = new SetMapControlPackStateUseCase(new TestMapControlPackStore());

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(
            "map-control.test",
            LocalCoachPackState.Draft));
    }
}
