namespace TempoCue.Application;

/// <summary>Удаляет локальный экземпляр Coach Pack из пользовательского хранилища.</summary>
public sealed class DeleteCoachPackUseCase
{
    private readonly ICoachPackStore _store;

    public DeleteCoachPackUseCase(ICoachPackStore store)
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
