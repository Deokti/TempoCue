namespace TempoCue.Application;

/// <summary>Проверяет переносимый набор контроля карты и безопасно устанавливает его локально.</summary>
public sealed class ImportMapControlPackUseCase
{
    private readonly IMapControlPackStore _store;
    private readonly HashSet<string> _builtInPackIds;

    public ImportMapControlPackUseCase(
        IMapControlPackStore store,
        IEnumerable<string> builtInPackIds)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(builtInPackIds);
        _builtInPackIds = builtInPackIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<MapControlPackImportPreview> InspectAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var pack = await MapControlPackLoader.LoadAsync(source, cancellationToken);
        return await InspectPackAsync(pack, cancellationToken);
    }

    public async Task<MapControlPackImportResult> ExecuteAsync(
        MapControlPackImportPreview approvedPreview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        var currentPreview = await InspectPackAsync(approvedPreview.Pack, cancellationToken);
        if (!currentPreview.CanImport
            || currentPreview.RequiresReplacement && !approvedPreview.RequiresReplacement)
        {
            return new MapControlPackImportResult(false, currentPreview, null);
        }

        var state = currentPreview.ExistingState ?? LocalCoachPackState.Disabled;
        var saveResult = await _store.ImportAsync(
            currentPreview.Pack,
            state,
            cancellationToken);
        return new MapControlPackImportResult(true, currentPreview, saveResult);
    }

    private async Task<MapControlPackImportPreview> InspectPackAsync(
        MapControlPack pack,
        CancellationToken cancellationToken)
    {
        if (_builtInPackIds.Contains(pack.Id))
        {
            return new MapControlPackImportPreview(
                pack,
                MapControlPackImportConflict.BuiltInPack,
                null);
        }

        var snapshot = await _store.LoadAsync(cancellationToken);
        var existingEntry = snapshot.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Pack.Id, pack.Id, StringComparison.OrdinalIgnoreCase));
        if (existingEntry is null)
        {
            return new MapControlPackImportPreview(
                pack,
                MapControlPackImportConflict.None,
                null);
        }

        return existingEntry.Origin == LocalCoachPackOrigin.Imported
            ? new MapControlPackImportPreview(
                pack,
                MapControlPackImportConflict.ImportedPack,
                existingEntry.State)
            : new MapControlPackImportPreview(
                pack,
                MapControlPackImportConflict.CreatedPack,
                existingEntry.State);
    }
}
