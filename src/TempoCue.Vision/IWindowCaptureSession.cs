namespace TempoCue.Vision;

/// <summary>
/// Предоставляет управляемый захват выбранного окна и неизменяемый диагностический снимок.
/// </summary>
public interface IWindowCaptureSession
{
    CaptureDiagnosticsSnapshot Snapshot { get; }

    /// <param name="isObservationWanted">
    /// Нужно ли сейчас наблюдение за миникартой: идёт ли матч и есть ли кому
    /// слушать результат. Захват не включается, пока признак ложен, и
    /// прекращается, если признак стал ложен посреди матча. Окно игры для
    /// этого не годится: оно существует с начала загрузки, а миникарта
    /// появляется много позже.
    ///
    /// Значение null означает «захватывать всегда» и оставлено для вызовов,
    /// которым состояние матча недоступно.
    /// </param>
    void EnableAutomaticLeagueWindowCapture(
        Func<bool>? isObservationWanted = null,
        CancellationToken cancellationToken = default);

    Task<bool> PickAndStartAsync(
        nint ownerWindow,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
