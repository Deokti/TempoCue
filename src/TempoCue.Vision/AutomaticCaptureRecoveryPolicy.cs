namespace TempoCue.Vision;

/// <summary>Определяет, когда автоматический захват нужно переподключить к окну матча.</summary>
internal static class AutomaticCaptureRecoveryPolicy
{
    internal static readonly TimeSpan StalledFrameTimeout = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Стоит ли сейчас цепляться к окну матча.
    /// </summary>
    /// <remarks>
    /// Окно игры существует с начала загрузки, а миникарта появляется много
    /// позже. Захват, включённый по одному лишь наличию окна, всё это время
    /// разбирает экран загрузки: расходует процессор впустую ровно тогда, когда
    /// он нужнее всего игре, и показывает в диагностике ошибку поиска области
    /// там, где ошибки нет.
    ///
    /// Поэтому решает не окно, а состояние матча. Признак начала — ответ
    /// Live Client Data: он приходит, когда игровой мир загружен, то есть
    /// примерно тогда же, когда на экране появляется миникарта.
    ///
    /// Перезапуск TempoCue внутри идущего матча этим не задерживается: там
    /// признак истинен с первого опроса, и захват включается сразу.
    /// </remarks>
    public static bool ShouldAttach(
        CaptureSessionState state,
        bool isMatchRunning,
        bool needsRestart)
    {
        if (!isMatchRunning)
        {
            return false;
        }

        // Захват идёт, но кадры встали — переподключение важнее прочих условий.
        return needsRestart
            || state is not CaptureSessionState.Capturing
                and not CaptureSessionState.SelectingTarget;
    }

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
