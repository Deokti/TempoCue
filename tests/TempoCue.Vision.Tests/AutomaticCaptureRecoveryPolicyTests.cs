using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

public sealed class AutomaticCaptureRecoveryPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ShouldRestart_ReturnsFalseBeforeFirstFrameGracePeriodExpires()
    {
        var result = AutomaticCaptureRecoveryPolicy.ShouldRestart(
            CaptureSessionState.Capturing,
            Now - TimeSpan.FromSeconds(3),
            lastFrameAt: null,
            Now);

        Assert.False(result);
    }

    [Fact]
    public void ShouldRestart_ReturnsTrueWhenFirstFrameNeverArrives()
    {
        var result = AutomaticCaptureRecoveryPolicy.ShouldRestart(
            CaptureSessionState.Capturing,
            Now - AutomaticCaptureRecoveryPolicy.StalledFrameTimeout,
            lastFrameAt: null,
            Now);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRestart_ReturnsTrueWhenExistingFrameStreamStops()
    {
        var result = AutomaticCaptureRecoveryPolicy.ShouldRestart(
            CaptureSessionState.Capturing,
            Now - TimeSpan.FromMinutes(1),
            Now - AutomaticCaptureRecoveryPolicy.StalledFrameTimeout,
            Now);

        Assert.True(result);
    }

    [Fact]
    public void ShouldRestart_IgnoresNonCapturingStates()
    {
        var result = AutomaticCaptureRecoveryPolicy.ShouldRestart(
            CaptureSessionState.Stopped,
            Now - TimeSpan.FromMinutes(1),
            Now - TimeSpan.FromMinutes(1),
            Now);

        Assert.False(result);
    }

    [Fact]
    public void ShouldReleaseMissingTarget_FreesActiveCaptureAfterMatchEnds()
    {
        // Завершённый матч закрывает окно, и переподключение его не находит.
        // Активный захват в этот момент обязан освободиться, иначе диагностика
        // навсегда остаётся в состоянии «кадры поступают» с замершими метриками.
        Assert.True(AutomaticCaptureRecoveryPolicy.ShouldReleaseMissingTarget(
            CaptureSessionState.Capturing));
    }

    [Theory]
    [InlineData(CaptureSessionState.Idle)]
    [InlineData(CaptureSessionState.Stopped)]
    [InlineData(CaptureSessionState.Failed)]
    [InlineData(CaptureSessionState.SelectingTarget)]
    [InlineData(CaptureSessionState.Unsupported)]
    public void ShouldReleaseMissingTarget_LeavesInactiveAndDeliberateStatesAlone(
        CaptureSessionState state)
    {
        // Остановку пользователем, ожидание окна и выбор цели трогать нельзя:
        // отсутствие окна для них является нормальным состоянием, а не событием.
        Assert.False(AutomaticCaptureRecoveryPolicy.ShouldReleaseMissingTarget(state));
    }

    [Theory]
    [InlineData(CaptureSessionState.Idle)]
    [InlineData(CaptureSessionState.Stopped)]
    [InlineData(CaptureSessionState.Failed)]
    [InlineData(CaptureSessionState.Capturing)]
    public void ShouldAttach_WaitsForTheMatchToStart(CaptureSessionState state)
    {
        // Окно игры существует с начала загрузки, а миникарты на экране ещё нет.
        // Разбирать в это время нечего, а процессор нужен самой игре.
        Assert.False(AutomaticCaptureRecoveryPolicy.ShouldAttach(
            state,
            isObservationWanted: false,
            needsRestart: false));
    }

    [Fact]
    public void ShouldAttach_IgnoresStalledFramesUntilTheMatchStarts()
    {
        // Переподключение при вставших кадрах важнее прочих условий, но не
        // важнее самого матча: цепляться на загрузке незачем в любом случае.
        Assert.False(AutomaticCaptureRecoveryPolicy.ShouldAttach(
            CaptureSessionState.Capturing,
            isObservationWanted: false,
            needsRestart: true));
    }

    [Theory]
    [InlineData(CaptureSessionState.Idle)]
    [InlineData(CaptureSessionState.Stopped)]
    [InlineData(CaptureSessionState.Failed)]
    public void ShouldAttach_StartsAsSoonAsTheMatchIsRunning(CaptureSessionState state)
    {
        // Этот же путь покрывает перезапуск TempoCue внутри идущего матча:
        // признак истинен с первого опроса, и ждать следующего матча не нужно.
        Assert.True(AutomaticCaptureRecoveryPolicy.ShouldAttach(
            state,
            isObservationWanted: true,
            needsRestart: false));
    }

    [Theory]
    [InlineData(CaptureSessionState.Capturing)]
    [InlineData(CaptureSessionState.SelectingTarget)]
    public void ShouldAttach_LeavesWorkingCaptureAlone(CaptureSessionState state)
    {
        // Захват идёт или окно выбирается — вмешиваться не во что.
        Assert.False(AutomaticCaptureRecoveryPolicy.ShouldAttach(
            state,
            isObservationWanted: true,
            needsRestart: false));
    }

    [Fact]
    public void ShouldAttach_ReconnectsStalledCaptureDuringMatch()
    {
        Assert.True(AutomaticCaptureRecoveryPolicy.ShouldAttach(
            CaptureSessionState.Capturing,
            isObservationWanted: true,
            needsRestart: true));
    }

    [Fact]
    public void ShouldReleaseUnwantedCapture_StopsCaptureWhenTheLastVisionRuleGoesOff()
    {
        // Наблюдение можно выключить посреди матча. Без этого захват дожил бы
        // до конца игры, разбирая кадры, результат которых некому слушать.
        Assert.True(AutomaticCaptureRecoveryPolicy.ShouldReleaseUnwantedCapture(
            CaptureSessionState.Capturing,
            isObservationWanted: false,
            startedAutomatically: true));
    }

    [Fact]
    public void ShouldReleaseUnwantedCapture_KeepsWindowChosenByThePlayer()
    {
        // Окно, выбранное вручную в диагностике, затеяно ради самой проверки
        // захвата. Гасить его за выключенные уведомления — отнимать инструмент.
        Assert.False(AutomaticCaptureRecoveryPolicy.ShouldReleaseUnwantedCapture(
            CaptureSessionState.Capturing,
            isObservationWanted: false,
            startedAutomatically: false));
    }

    [Fact]
    public void ShouldReleaseUnwantedCapture_LeavesWantedCaptureAlone()
    {
        Assert.False(AutomaticCaptureRecoveryPolicy.ShouldReleaseUnwantedCapture(
            CaptureSessionState.Capturing,
            isObservationWanted: true,
            startedAutomatically: true));
    }

    [Theory]
    [InlineData(CaptureSessionState.Idle)]
    [InlineData(CaptureSessionState.Stopped)]
    [InlineData(CaptureSessionState.Failed)]
    [InlineData(CaptureSessionState.SelectingTarget)]
    [InlineData(CaptureSessionState.Unsupported)]
    public void ShouldReleaseUnwantedCapture_HasNothingToReleaseOutsideCapture(
        CaptureSessionState state)
    {
        Assert.False(AutomaticCaptureRecoveryPolicy.ShouldReleaseUnwantedCapture(
            state,
            isObservationWanted: false,
            startedAutomatically: true));
    }
}
