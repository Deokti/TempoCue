namespace TempoCue.Application.Tests;

internal sealed class TestMapControlPackStore : IMapControlPackStore
{
    private readonly List<LocalMapControlPackEntry> _entries = [];
    private readonly Dictionary<string, LocalCoachPackState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    internal void Add(
        MapControlPack pack,
        LocalCoachPackState state,
        LocalCoachPackOrigin origin)
    {
        _entries.RemoveAll(entry => string.Equals(
            entry.Pack.Id,
            pack.Id,
            StringComparison.OrdinalIgnoreCase));
        _entries.Add(new LocalMapControlPackEntry(
            pack,
            state,
            DateTimeOffset.UtcNow,
            origin));
        _states[pack.Id] = state;
    }

    public Task<MapControlPackLibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new MapControlPackLibrarySnapshot(
            _entries.ToArray(),
            0,
            new Dictionary<string, LocalCoachPackState>(
                _states,
                StringComparer.OrdinalIgnoreCase)));
    }

    public Task<MapControlPackSaveResult> SaveAsync(
        MapControlPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Add(pack, state, LocalCoachPackOrigin.CreatedByUser);
        return Task.FromResult(new MapControlPackSaveResult(pack.Id, state, pack.Id));
    }

    public Task<MapControlPackSaveResult> ImportAsync(
        MapControlPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Add(pack, state, LocalCoachPackOrigin.Imported);
        return Task.FromResult(new MapControlPackSaveResult(pack.Id, state, pack.Id));
    }

    public Task SetStateAsync(
        string packId,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _states[packId] = state;
        var index = _entries.FindIndex(entry => string.Equals(
            entry.Pack.Id,
            packId,
            StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _entries[index] = _entries[index] with
            {
                State = state,
                SavedAtUtc = DateTimeOffset.UtcNow
            };
        }

        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(
        string packId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var removed = _entries.RemoveAll(entry => string.Equals(
            entry.Pack.Id,
            packId,
            StringComparison.OrdinalIgnoreCase)) > 0;
        _states.Remove(packId);
        return Task.FromResult(removed);
    }
}
