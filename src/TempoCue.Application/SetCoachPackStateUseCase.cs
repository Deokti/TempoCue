namespace TempoCue.Application;

/// <summary>Изменяет локальное состояние Coach Pack, не переписывая его переносимое содержимое.</summary>
public sealed class SetCoachPackStateUseCase
{
    private readonly ICoachPackStore _store;

    public SetCoachPackStateUseCase(ICoachPackStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task ExecuteAsync(
        string packId,
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        if (state == LocalCoachPackState.Draft)
        {
            throw new ArgumentException(
                "Переключатель может только включить или выключить сохранённый Coach Pack.",
                nameof(state));
        }

        return _store.SetStateAsync(packId, state, cancellationToken);
    }
}
