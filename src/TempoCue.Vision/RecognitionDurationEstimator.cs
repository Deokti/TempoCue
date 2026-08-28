namespace TempoCue.Vision;

/// <summary>
/// Среднее время распознавания кадра по последним замерам. Одиночное значение
/// слишком дёргается, чтобы по нему настраивать нагрузку, а среднее за всё время
/// не показывает, что стало после смены числа потоков или разрешения.
/// </summary>
internal sealed class RecognitionDurationEstimator
{
    private const int WindowSize = 16;

    private readonly object _sync = new();
    private readonly double[] _samples = new double[WindowSize];
    private int _nextIndex;
    private int _count;
    private double _sum;

    public double AverageMilliseconds
    {
        get
        {
            lock (_sync)
            {
                return _count == 0 ? 0 : _sum / _count;
            }
        }
    }

    public void Register(TimeSpan duration)
    {
        var milliseconds = duration.TotalMilliseconds;
        if (milliseconds < 0)
        {
            return;
        }

        lock (_sync)
        {
            if (_count == WindowSize)
            {
                _sum -= _samples[_nextIndex];
            }
            else
            {
                _count++;
            }

            _samples[_nextIndex] = milliseconds;
            _sum += milliseconds;
            _nextIndex = (_nextIndex + 1) % WindowSize;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            Array.Clear(_samples);
            _nextIndex = 0;
            _count = 0;
            _sum = 0;
        }
    }
}
