using System.Diagnostics;
using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

public sealed class CaptureFrameMetricsTests
{
    [Fact]
    public void RegisterFrame_SeparatesSelectedAndRateLimitedFrames()
    {
        var metrics = new CaptureFrameMetrics(
            selectionFrameRateLimit: 30,
            previewFrameRateLimit: 4);
        var capturedAt = new DateTimeOffset(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);
        var selectionInterval = (long)Math.Ceiling(Stopwatch.Frequency / 30d);

        var first = metrics.RegisterFrame(1920, 1080, 100, capturedAt);
        var limited = metrics.RegisterFrame(
            1920,
            1080,
            100 + (selectionInterval / 2),
            capturedAt.AddMilliseconds(10));
        var selected = metrics.RegisterFrame(
            1920,
            1080,
            100 + selectionInterval,
            capturedAt.AddMilliseconds(34));

        Assert.True(first.IsSelected);
        Assert.True(first.ShouldCreatePreview);
        Assert.False(limited.IsSelected);
        Assert.False(limited.ShouldCreatePreview);
        Assert.True(selected.IsSelected);
        Assert.False(selected.ShouldCreatePreview);

        var snapshot = metrics.Snapshot;
        Assert.Equal(3, snapshot.ReceivedFrameCount);
        Assert.Equal(2, snapshot.SelectedFrameCount);
        Assert.Equal(1, snapshot.RateLimitedFrameCount);
        Assert.Equal(capturedAt.AddMilliseconds(34), snapshot.LastFrameAt);
    }

    [Fact]
    public void RegisterFrame_ReportsContentSizeChangesAfterInitialSize()
    {
        var metrics = new CaptureFrameMetrics(30, 4);
        metrics.SetInitialFrameSize(1920, 1080);

        var unchanged = metrics.RegisterFrame(
            1920,
            1080,
            timestamp: 100,
            DateTimeOffset.UtcNow);
        var changed = metrics.RegisterFrame(
            2560,
            1440,
            timestamp: 200,
            DateTimeOffset.UtcNow);

        Assert.False(unchanged.IsSizeChanged);
        Assert.True(changed.IsSizeChanged);
        Assert.Equal(2560, metrics.Snapshot.FrameWidth);
        Assert.Equal(1440, metrics.Snapshot.FrameHeight);
    }
}
