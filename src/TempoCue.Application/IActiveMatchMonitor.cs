namespace TempoCue.Application;

/// <summary>
/// Предоставляет единый поток обновлений активного матча нескольким потребителям.
/// Реализация владеет одним циклом polling независимо от количества подписчиков.
/// </summary>
public interface IActiveMatchMonitor
{
    event EventHandler<ActiveMatchUpdateEventArgs>? Updated;

    ActiveMatchUpdate? CurrentUpdate { get; }

    Task RefreshAsync(CancellationToken cancellationToken = default);
}
