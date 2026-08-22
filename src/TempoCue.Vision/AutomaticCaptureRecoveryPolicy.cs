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

    /// <summary>
    /// Определяет, нужно ли освободить захват, когда окно матча больше не найдено.
    /// Без этого завершённый матч оставлял диагностику в состоянии «кадры поступают»
    /// с замершими метриками: попытка переподключения просто не находила окно и
    /// молча возвращала управление, не трогая состояние.
    /// </summary>
    public static bool ShouldReleaseMissingTarget(CaptureSessionState state) =>
        state is CaptureSessionState.Capturing;
}
