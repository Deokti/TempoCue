using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Сохраняет последний Coach Pack в памяти для тестов UI и ViewModel.</summary>
internal sealed class TestCoachPackStore : ICoachPackStore
{
    private readonly List<LocalCoachPackEntry> _entries = [];
    private readonly Dictionary<string, LocalCoachPackState> _states =
        new(StringComparer.OrdinalIgnoreCase);

    public CoachPack? SavedPack { get; private set; }

    public LocalCoachPackState? SavedState { get; private set; }

    public Task<CoachPackLibrarySnapshot> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CoachPackLibrarySnapshot(
            _entries.ToArray(),
            0,
            new Dictionary<string, LocalCoachPackState>(_states, StringComparer.OrdinalIgnoreCase)));
    }

    public Task<CoachPackSaveResult> SaveAsync(
        CoachPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SavedPack = pack;
        SavedState = state;
        _entries.RemoveAll(entry => string.Equals(
            entry.Pack.Id,
            pack.Id,
            StringComparison.OrdinalIgnoreCase));
        _entries.Add(new LocalCoachPackEntry(pack, state, DateTimeOffset.UtcNow));
        _states[pack.Id] = state;
        return Task.FromResult(new CoachPackSaveResult(pack.Id, state, $"C:\\Test\\{pack.Id}.json"));
    }

    public Task SetStateAsync(
        string packId,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _states[packId] = state;
        var entryIndex = _entries.FindIndex(entry => string.Equals(
            entry.Pack.Id,
            packId,
            StringComparison.OrdinalIgnoreCase));
        if (entryIndex >= 0)
        {
            var entry = _entries[entryIndex];
            _entries[entryIndex] = entry with
            {
                State = state,
                SavedAtUtc = DateTimeOffset.UtcNow
            };
        }

        return Task.CompletedTask;
    }
}
