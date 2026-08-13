namespace TempoCue.Application;

/// <summary>Удаляет локальный экземпляр набора контроля карты.</summary>
public sealed class DeleteMapControlPackUseCase
{
    private readonly IMapControlPackStore _store;

    public DeleteMapControlPackUseCase(IMapControlPackStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<bool> ExecuteAsync(
        string packId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        return _store.DeleteAsync(packId, cancellationToken);
    }
}
