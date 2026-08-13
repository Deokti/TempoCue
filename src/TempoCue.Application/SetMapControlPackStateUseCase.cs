namespace TempoCue.Application;

/// <summary>Включает или выключает сохранённый набор контроля карты.</summary>
public sealed class SetMapControlPackStateUseCase
{
    private readonly IMapControlPackStore _store;

    public SetMapControlPackStateUseCase(IMapControlPackStore store)
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
                "Переключатель может только включить или выключить сохранённый Map Control Pack.",
                nameof(state));
        }

        return _store.SetStateAsync(packId, state, cancellationToken);
    }
}
