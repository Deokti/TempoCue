namespace TempoCue.Vision;

/// <summary>
/// Предоставляет управляемый захват выбранного окна и неизменяемый диагностический снимок.
/// </summary>
public interface IWindowCaptureSession
{
    CaptureDiagnosticsSnapshot Snapshot { get; }

    Task<bool> PickAndStartAsync(
        nint ownerWindow,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
