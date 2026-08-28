using System.Diagnostics;

namespace TempoCue.Vision.Tests;

public sealed class FrameRateEstimatorTests
{
    [Fact]
    public void RegisterFrame_PublishesRateOnlyAfterCompleteSampleWindow()
    {
        var estimator = new FrameRateEstimator(0);

        estimator.RegisterFrame(Stopwatch.Frequency / 2);

        Assert.Equal(0, estimator.FramesPerSecond);

        estimator.RegisterFrame(Stopwatch.Frequency);

        Assert.Equal(2, estimator.FramesPerSecond, precision: 3);
    }

    [Fact]
    public void RegisterFrame_StartsNewWindowAfterPublishingRate()
    {
        var estimator = new FrameRateEstimator(0);
        estimator.RegisterFrame(Stopwatch.Frequency / 2);
        estimator.RegisterFrame(Stopwatch.Frequency);

        estimator.RegisterFrame(Stopwatch.Frequency * 2);

        Assert.Equal(1, estimator.FramesPerSecond, precision: 3);
    }
}
