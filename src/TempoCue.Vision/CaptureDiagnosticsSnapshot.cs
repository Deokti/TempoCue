namespace TempoCue.Vision;

/// <summary>Неизменяемый снимок состояния диагностического захвата экрана.</summary>
public sealed record CaptureDiagnosticsSnapshot(
    CaptureSessionState State,
    string? TargetName,
    int FrameWidth,
    int FrameHeight,
    long ReceivedFrameCount,
    double FramesPerSecond,
    int SelectionFrameRateLimit,
    long SelectedFrameCount,
    double SelectionFramesPerSecond,
    long RateLimitedFrameCount,
    DateTimeOffset? LastFrameAt,
    string? ErrorMessage);
