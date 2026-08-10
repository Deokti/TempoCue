using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Сохраняет последний Coach Pack в памяти для тестов UI и ViewModel.</summary>
internal sealed class TestCoachPackStore : ICoachPackStore
{
    public CoachPack? SavedPack { get; private set; }

    public LocalCoachPackState? SavedState { get; private set; }

    public Task<CoachPackSaveResult> SaveAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SavedPack = pack;
        SavedState = state;
        return Task.FromResult(new CoachPackSaveResult(pack.Id, state, $"C:\\Test\\{pack.Id}.json"));
    }
}
