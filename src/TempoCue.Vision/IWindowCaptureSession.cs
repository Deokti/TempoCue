namespace TempoCue.Vision;

/// <summary>
/// Предоставляет управляемый захват выбранного окна и неизменяемый диагностический снимок.
/// </summary>
public interface IWindowCaptureSession
{
    CaptureDiagnosticsSnapshot Snapshot { get; }

    /// <param name="isMatchRunning">
    /// Идёт ли матч. Захват не включается, пока признак ложен: окно игры
    /// существует с начала загрузки, а миникарта появляется много позже.
    /// Значение null означает «захватывать всегда» и оставлено для вызовов,
    /// которым состояние матча недоступно.
    /// </param>
    void EnableAutomaticLeagueWindowCapture(
        Func<bool>? isMatchRunning = null,
        CancellationToken cancellationToken = default);

    Task<bool> PickAndStartAsync(
        nint ownerWindow,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
