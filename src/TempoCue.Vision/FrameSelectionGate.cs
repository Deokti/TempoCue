using System.Diagnostics;

namespace TempoCue.Vision;

/// <summary>Отбирает кадры по монотонному времени без очереди и накопления долга.</summary>
internal sealed class FrameSelectionGate
{
    private readonly long _minimumIntervalTicks;
    private bool _hasSelectedFrame;
    private long _nextAllowedTimestamp;

    public FrameSelectionGate(int maximumFramesPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFramesPerSecond);
        MaximumFramesPerSecond = maximumFramesPerSecond;
        _minimumIntervalTicks = (long)Math.Ceiling(
            Stopwatch.Frequency / (double)maximumFramesPerSecond);
    }

    public int MaximumFramesPerSecond { get; }

    public bool TrySelect() => TrySelect(Stopwatch.GetTimestamp());

    internal bool TrySelect(long timestamp)
    {
        if (!_hasSelectedFrame)
        {
            _hasSelectedFrame = true;
            _nextAllowedTimestamp = AddInterval(timestamp);
            return true;
        }

        if (timestamp < _nextAllowedTimestamp)
        {
            return false;
        }

        var delay = timestamp - _nextAllowedTimestamp;
        _nextAllowedTimestamp = delay >= _minimumIntervalTicks
            ? AddInterval(timestamp)
            : AddInterval(_nextAllowedTimestamp);
        return true;
    }

    public void Reset()
    {
        _hasSelectedFrame = false;
        _nextAllowedTimestamp = 0;
    }

    private long AddInterval(long timestamp) =>
        timestamp > long.MaxValue - _minimumIntervalTicks
            ? long.MaxValue
            : timestamp + _minimumIntervalTicks;
}
