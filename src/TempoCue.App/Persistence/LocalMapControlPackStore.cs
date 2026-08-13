using TempoCue.Application;

namespace TempoCue.App.Persistence;

/// <summary>Хранит пользовательские наборы контроля карты и их локальное состояние.</summary>
public sealed class LocalMapControlPackStore : IMapControlPackStore
{
    private readonly LocalCoachingPackFileStore<MapControlPack> _files;

    public LocalMapControlPackStore(string rootDirectory)
    {
        _files = new LocalCoachingPackFileStore<MapControlPack>(
            rootDirectory,
            "Map Control Pack",
            "map-control-pack",
            static pack => pack.Id,
            static pack => pack.Name,
            MapControlPackValidator.Validate,
            MapControlPackLoader.LoadAsync,
            static (stream, pack, cancellationToken) =>
                MapControlPackWriter.WriteAsync(stream, pack, cancellationToken));
    }

    public async Task<MapControlPackLibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _files.LoadAsync(cancellationToken);
        return new MapControlPackLibrarySnapshot(
            snapshot.Entries
                .Select(static entry => new LocalMapControlPackEntry(
                    entry.Pack,
                    entry.State,
                    entry.SavedAtUtc,
                    entry.Origin))
                .ToArray(),
            snapshot.InvalidFileCount,
            snapshot.PackStates);
    }

    public async Task<MapControlPackSaveResult> SaveAsync(
        MapControlPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        var result = await _files.SaveAsync(
            pack,
            state,
            LocalCoachPackOrigin.CreatedByUser,
            cancellationToken);
        return new MapControlPackSaveResult(result.PackId, result.State, result.DisplayPath);
    }

    public async Task<MapControlPackSaveResult> ImportAsync(
        MapControlPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        var result = await _files.SaveAsync(
            pack,
            state,
            LocalCoachPackOrigin.Imported,
            cancellationToken);
        return new MapControlPackSaveResult(result.PackId, result.State, result.DisplayPath);
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
