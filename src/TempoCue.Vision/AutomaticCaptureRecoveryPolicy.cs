namespace TempoCue.Vision;

/// <summary>Определяет, когда автоматический захват нужно переподключить к окну матча.</summary>
internal static class AutomaticCaptureRecoveryPolicy
{
    internal static readonly TimeSpan StalledFrameTimeout = TimeSpan.FromSeconds(4);

    public static bool ShouldRestart(
        CaptureSessionState state,
        DateTimeOffset? captureStartedAt,
        DateTimeOffset? lastFrameAt,
        DateTimeOffset now)
    {
        if (state is not CaptureSessionState.Capturing || captureStartedAt is null)
        {
            return false;
        }

        var lastActivityAt = lastFrameAt is { } frameAt && frameAt > captureStartedAt
            ? frameAt
            : captureStartedAt.Value;
        return now - lastActivityAt >= StalledFrameTimeout;
    }
}
