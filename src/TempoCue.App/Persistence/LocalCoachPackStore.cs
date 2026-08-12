using TempoCue.Application;

namespace TempoCue.App.Persistence;

/// <summary>Хранит пользовательские матчап-наборы и их локальное состояние.</summary>
public sealed class LocalCoachPackStore : ICoachPackStore
{
    private readonly LocalCoachingPackFileStore<CoachPack> _files;

    public LocalCoachPackStore(string rootDirectory)
    {
        _files = new LocalCoachingPackFileStore<CoachPack>(
            rootDirectory,
            "Coach Pack",
            "coach-pack",
            static pack => pack.Id,
            static pack => pack.Name,
            CoachPackValidator.Validate,
            CoachPackLoader.LoadAsync,
            static (stream, pack, cancellationToken) =>
                CoachPackWriter.WriteAsync(stream, pack, cancellationToken));
    }

    public async Task<CoachPackLibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _files.LoadAsync(cancellationToken);
        return new CoachPackLibrarySnapshot(
            snapshot.Entries
                .Select(static entry => new LocalCoachPackEntry(
                    entry.Pack,
                    entry.State,
                    entry.SavedAtUtc,
                    entry.Origin))
                .ToArray(),
            snapshot.InvalidFileCount,
            snapshot.PackStates);
    }

    public async Task<CoachPackSaveResult> SaveAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        var result = await _files.SaveAsync(
            pack,
            state,
            LocalCoachPackOrigin.CreatedByUser,
            cancellationToken);
        return new CoachPackSaveResult(result.PackId, result.State, result.DisplayPath);
    }

    public async Task<CoachPackSaveResult> ImportAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        var result = await _files.SaveAsync(
            pack,
            state,
            LocalCoachPackOrigin.Imported,
            cancellationToken);
        return new CoachPackSaveResult(result.PackId, result.State, result.DisplayPath);
    }

    public Task SetStateAsync(
        string packId,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default) =>
        _files.SetStateAsync(packId, state, cancellationToken);

    public Task<bool> DeleteAsync(
        string packId,
        CancellationToken cancellationToken = default) =>
        _files.DeleteAsync(packId, cancellationToken);
}
