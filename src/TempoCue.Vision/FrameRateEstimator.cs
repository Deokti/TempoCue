using System.Diagnostics;

namespace TempoCue.Vision;

internal sealed class FrameRateEstimator
{
    private readonly object _sync = new();
    private long _sampleStartedAt;
    private int _sampleFrameCount;
    private double _framesPerSecond;

    public FrameRateEstimator() => Reset();

    internal FrameRateEstimator(long sampleStartedAt) => _sampleStartedAt = sampleStartedAt;

    public double FramesPerSecond
    {
        get
        {
            lock (_sync)
            {
                return _framesPerSecond;
            }
        }
    }

    public void RegisterFrame() => RegisterFrame(Stopwatch.GetTimestamp());

    internal void RegisterFrame(long timestamp)
    {
        lock (_sync)
        {
            _sampleFrameCount++;
            var elapsedSeconds = (timestamp - _sampleStartedAt) / (double)Stopwatch.Frequency;
            if (elapsedSeconds < 1)
            {
                return;
            }

            _framesPerSecond = _sampleFrameCount / elapsedSeconds;
            _sampleStartedAt = timestamp;
            _sampleFrameCount = 0;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _sampleStartedAt = Stopwatch.GetTimestamp();
            _sampleFrameCount = 0;
            _framesPerSecond = 0;
        }
    }
}
