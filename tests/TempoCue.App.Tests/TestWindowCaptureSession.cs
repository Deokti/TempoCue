using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>Управляемая сессия захвата для тестов ViewModel и WPF без системного пикера.</summary>
internal sealed class TestWindowCaptureSession : IWindowCaptureSession
{
    public CaptureDiagnosticsSnapshot Snapshot { get; private set; } =
        TestCaptureDiagnosticsSnapshot.Create();

    public int PickCount { get; private set; }

    public int StopCount { get; private set; }

    public nint LastOwnerWindow { get; private set; }

    public void EnableAutomaticLeagueWindowCapture(
        CancellationToken cancellationToken = default)
    {
    }

    public Task<bool> PickAndStartAsync(
        nint ownerWindow,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PickCount++;
        LastOwnerWindow = ownerWindow;
        return Task.FromResult(true);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        StopCount++;
        return Task.CompletedTask;
    }

    public void SetSnapshot(CaptureDiagnosticsSnapshot snapshot) =>
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
}
