using System.Diagnostics;
using TempoCue.Vision;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Долгий прогон распознавания на одном и том же кадре. Короткий замер показывает
/// стоимость кадра, но не показывает накопление: игрок наблюдает деградацию за
/// тридцать минут, а десять повторов подряд её не воспроизведут.
///
/// Кадр один и тот же намеренно: если стоимость растёт при неизменном входе,
/// причина внутри приложения, а не в содержимом карты.
/// </summary>
internal static class RecognitionSoak
{
    private static readonly TimeSpan ReportInterval = TimeSpan.FromSeconds(30);

    public static async Task<int> RunAsync(string samplePath, int minutes, int framesPerSecond)
    {
        var sample = await ReplaySampleFile.LoadAsync(samplePath);
        var runner = new MinimapReplayRunner();
        var interval = TimeSpan.FromSeconds(1d / framesPerSecond);
        var deadline = Stopwatch.StartNew();
        var total = TimeSpan.FromMinutes(minutes);

        Console.WriteLine(
            $"Прогон {minutes} мин на кадре {ReplaySampleFile.Name(samplePath)} " +
            $"с частотой {framesPerSecond} кадр/с.");
        Console.WriteLine();
        Console.WriteLine(
            "  минута | медиана | p90    | куча, МБ | процесс, МБ | сборок 0/1/2");

        var window = new List<double>();
        var nextReport = ReportInterval;
        var frames = 0L;
        while (deadline.Elapsed < total)
        {
            var frameStarted = Stopwatch.GetTimestamp();
            var stopwatch = Stopwatch.StartNew();
            runner.Run(sample);
            stopwatch.Stop();
            window.Add(stopwatch.Elapsed.TotalMilliseconds);
            frames++;

            if (deadline.Elapsed >= nextReport)
            {
                Report(deadline.Elapsed, window);
                window.Clear();
                nextReport += ReportInterval;
            }

            // Воспроизводим живой ритм: в приложении кадры приходят по расписанию,
            // а не вплотную, и пауза между ними даёт сборщику мусора работать так
            // же, как в матче.
            var spent = Stopwatch.GetElapsedTime(frameStarted);
            if (spent < interval)
            {
                await Task.Delay(interval - spent);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"всего кадров: {frames}");
        return 0;
    }

    private static void Report(TimeSpan elapsed, IReadOnlyList<double> window)
    {
        if (window.Count == 0)
        {
            return;
        }

        var sorted = window.OrderBy(static value => value).ToArray();
        var process = Process.GetCurrentProcess();
        Console.WriteLine(
            $"  {elapsed.TotalMinutes,6:F1} | {sorted[sorted.Length / 2],7:F1} | " +
            $"{sorted[(int)(sorted.Length * 0.9)],6:F1} | " +
            $"{GC.GetTotalMemory(false) / 1024d / 1024d,8:F1} | " +
            $"{process.PrivateMemorySize64 / 1024d / 1024d,11:F1} | " +
            $"{GC.CollectionCount(0)}/{GC.CollectionCount(1)}/{GC.CollectionCount(2)}");
    }
}
