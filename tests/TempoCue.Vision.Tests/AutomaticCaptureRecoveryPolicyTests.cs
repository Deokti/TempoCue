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
}
