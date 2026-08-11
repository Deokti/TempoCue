using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Проверяет переносимый Coach Pack и устанавливает его как импортированный набор.</summary>
public sealed class ImportCoachPackUseCase
{
    private readonly ICoachPackStore _store;
    private readonly HashSet<string> _builtInPackIds;

    public ImportCoachPackUseCase(
        ICoachPackStore store,
        IEnumerable<string> builtInPackIds)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        ArgumentNullException.ThrowIfNull(builtInPackIds);
        _builtInPackIds = builtInPackIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<CoachPackImportPreview> InspectAsync(
        Stream source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        var pack = await CoachPackLoader.LoadAsync(source, cancellationToken);
        return await InspectPackAsync(pack, cancellationToken);
    }

    public async Task<CoachPackImportResult> ExecuteAsync(
        CoachPackImportPreview approvedPreview,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(approvedPreview);
        var currentPreview = await InspectPackAsync(
            approvedPreview.Pack,
            cancellationToken);

        // Между предпросмотром и подтверждением библиотека могла измениться.
        // Новую замену нельзя выполнять без отдельного согласия пользователя.
        if (!currentPreview.CanImport
            || currentPreview.RequiresReplacement && !approvedPreview.RequiresReplacement)
        {
            return new CoachPackImportResult(false, currentPreview, null);
        }

        var state = currentPreview.ExistingState ?? LocalCoachPackState.Disabled;
        var saveResult = await _store.ImportAsync(
            currentPreview.Pack,
            state,
            cancellationToken);
        return new CoachPackImportResult(true, currentPreview, saveResult);
    }

    private async Task<CoachPackImportPreview> InspectPackAsync(
        CoachPack pack,
        CancellationToken cancellationToken)
    {
        if (_builtInPackIds.Contains(pack.Id))
        {
            return new CoachPackImportPreview(
                pack,
                CoachPackImportConflict.BuiltInPack,
                null);
        }

        var snapshot = await _store.LoadAsync(cancellationToken);
        var existingEntry = snapshot.Entries.FirstOrDefault(entry =>
            string.Equals(entry.Pack.Id, pack.Id, StringComparison.OrdinalIgnoreCase));
        if (existingEntry is null)
        {
            return new CoachPackImportPreview(pack, CoachPackImportConflict.None, null);
        }

        return existingEntry.Origin == LocalCoachPackOrigin.Imported
            ? new CoachPackImportPreview(
                pack,
                CoachPackImportConflict.ImportedPack,
                existingEntry.State)
            : new CoachPackImportPreview(
                pack,
                CoachPackImportConflict.CreatedPack,
                existingEntry.State);
    }
}
