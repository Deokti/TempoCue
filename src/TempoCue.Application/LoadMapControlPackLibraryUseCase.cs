namespace TempoCue.Application;

/// <summary>Получает проверенные наборы контроля карты из настроенного хранилища.</summary>
public sealed class LoadMapControlPackLibraryUseCase
{
    private readonly IMapControlPackStore _store;

    public LoadMapControlPackLibraryUseCase(IMapControlPackStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<MapControlPackLibrarySnapshot> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        _store.LoadAsync(cancellationToken);
}
