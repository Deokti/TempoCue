namespace TempoCue.Application;

/// <summary>Проверяет и сохраняет пользовательский набор контроля карты.</summary>
public sealed class SaveMapControlPackUseCase
{
    private readonly IMapControlPackStore _store;

    public SaveMapControlPackUseCase(IMapControlPackStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<MapControlPackSaveResult> ExecuteAsync(
        MapControlPack pack,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pack);
        MapControlPackValidator.Validate(pack);
        return _store.SaveAsync(pack, state, cancellationToken);
    }
}
