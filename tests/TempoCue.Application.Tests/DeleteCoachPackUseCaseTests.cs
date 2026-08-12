using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class DeleteCoachPackUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_DelegatesValidatedPackIdToStore()
    {
        var store = new RecordingCoachPackStore();
        var useCase = new DeleteCoachPackUseCase(store);

        var deleted = await useCase.ExecuteAsync("matchup.urgot-vs-sett");

        Assert.True(deleted);
        Assert.Equal("matchup.urgot-vs-sett", store.DeletedPackId);
    }

    private sealed class RecordingCoachPackStore : ICoachPackStore
    {
        public string? DeletedPackId { get; private set; }

        public Task<CoachPackLibrarySnapshot> LoadAsync(
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CoachPackSaveResult> SaveAsync(
            CoachPack pack,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<CoachPackSaveResult> ImportAsync(
            CoachPack pack,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task SetStateAsync(
            string packId,
            LocalCoachPackState state,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            string packId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DeletedPackId = packId;
            return Task.FromResult(true);
        }
    }
}
