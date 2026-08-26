using TempoCue.Vision;

namespace TempoCue.App.Tests;

/// <summary>Управляемая сессия захвата для тестов ViewModel и WPF без системного пикера.</summary>
internal sealed class TestWindowCaptureSession :
    IWindowCaptureSession,
    IMinimapReplaySampleSource
{
    public CaptureDiagnosticsSnapshot Snapshot { get; private set; } =
        TestCaptureDiagnosticsSnapshot.Create();

    public int PickCount { get; private set; }

    public int StopCount { get; private set; }

    public nint LastOwnerWindow { get; private set; }

    public MinimapReplaySample? ReplaySample { get; private set; }

    public bool HasReplaySample => ReplaySample is not null;

    public void EnableAutomaticLeagueWindowCapture(
        Func<bool>? isMatchRunning = null,
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

    public void SetReplaySample(MinimapReplaySample sample) =>
        ReplaySample = sample ?? throw new ArgumentNullException(nameof(sample));

    public MinimapReplaySample? CreateReplaySample() => ReplaySample;

    public bool IsRecordingSequence => RecordedFrameCount < RequestedSequenceFrames;

    public int RecordedFrameCount { get; private set; }

    /// <summary>Сколько кадров запросили последним вызовом: проверяется в тестах.</summary>
    public int RequestedSequenceFrames { get; private set; }

    public MinimapReplaySequence? RecordedSequence { get; set; }

    public void StartSequenceRecording(int frameCount)
    {
        RequestedSequenceFrames = frameCount;
        RecordedFrameCount = 0;
    }

    /// <summary>Имитирует набор кадров записью без настоящего захвата.</summary>
    public void CompleteSequenceRecording(MinimapReplaySequence sequence)
    {
        RecordedSequence = sequence;
        RecordedFrameCount = sequence.Frames.Count;
    }

    public MinimapReplaySequence? TakeRecordedSequence()
    {
        var sequence = RecordedSequence;
        RecordedSequence = null;
        RecordedFrameCount = 0;
        RequestedSequenceFrames = 0;
        return sequence;
    }
}
