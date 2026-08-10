namespace TempoCue.Application;

/// <summary>Получает проверенные пользовательские наборы из настроенного хранилища.</summary>
public sealed class LoadCoachPackLibraryUseCase
{
    private readonly ICoachPackStore _store;

    public LoadCoachPackLibraryUseCase(ICoachPackStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<CoachPackLibrarySnapshot> ExecuteAsync(
        CancellationToken cancellationToken = default) =>
        _store.LoadAsync(cancellationToken);
}
