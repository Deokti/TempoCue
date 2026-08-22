using System.Diagnostics;
using TempoCue.Vision;
using TempoCue.Vision.DeeperLeague;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Измеряет стоимость одного кадра в установившемся режиме. Первый прогон каждого
/// кадра отбрасывается: в матче ростер не меняется, поэтому шаблоны эталонов
/// готовятся один раз, и включать их подготовку в стоимость кадра неверно.
/// Полученные числа определяют, какую частоту разбора имеет смысл разрешать.
/// </summary>
internal static class RecognitionTiming
{
    private const int WarmupRuns = 2;
    private const int MeasuredRuns = 10;

    public static async Task<int> RunAsync(string corpusDirectory, string? modelDirectory)
    {
        var runner = new MinimapReplayRunner();
        using var model = modelDirectory is null
            ? null
            : new DeeperLeagueMinimapRecognizer(
                Path.Combine(modelDirectory, "best.onnx"),
                Path.Combine(modelDirectory, "champMap.json"));

        var ourCold = new List<double>();
        var ourWarm = new List<double>();
        var modelWarm = new List<double>();

        Console.WriteLine(
            $"Прогрев {WarmupRuns}, замер {MeasuredRuns} повторов на кадр.");
        Console.WriteLine();
        Console.WriteLine("кадр                                   | наш холод | наш тепл | модель");

        foreach (var path in ReplaySampleFile.EnumerateCorpus(corpusDirectory))
        {
            var sample = await ReplaySampleFile.LoadAsync(path);

            var cold = Measure(() => runner.Run(sample), 1);
            ourCold.Add(cold[0]);

            Measure(() => runner.Run(sample), WarmupRuns);
            var warm = Measure(() => runner.Run(sample), MeasuredRuns);
            ourWarm.AddRange(warm);

            var modelText = "—";
            if (model is not null)
            {
                Measure(() => Recognize(model, sample), WarmupRuns);
                var modelRuns = Measure(() => Recognize(model, sample), MeasuredRuns);
                modelWarm.AddRange(modelRuns);
                modelText = $"{Median(modelRuns):F1}";
            }

            Console.WriteLine(
                $"{ReplaySampleFile.Name(path),-38} | {cold[0],9:F1} | {Median(warm),8:F1} | {modelText,6}");
        }

        Console.WriteLine();
        Report("наш конвейер, первый кадр ростера", ourCold);
        Report("наш конвейер, установившийся", ourWarm);
        if (modelWarm.Count > 0)
        {
            Report("модель DeeperLeague", modelWarm);
        }

        Console.WriteLine();
        PrintAffordableRate("наш конвейер", ourWarm);
        if (modelWarm.Count > 0)
        {
            PrintAffordableRate("модель DeeperLeague", modelWarm);
        }

        return 0;
    }

    private static void Recognize(
        DeeperLeagueMinimapRecognizer model,
        MinimapReplaySample sample) =>
        model.Recognize(
            sample.BgraPixels.Span,
            sample.ImageWidth,
            sample.ImageHeight,
            sample.ImageStride,
            sample.ChampionReferences);

    private static double[] Measure(Action action, int runs)
    {
        var samples = new double[runs];
        for (var index = 0; index < runs; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            action();
            stopwatch.Stop();
            samples[index] = stopwatch.Elapsed.TotalMilliseconds;
        }

        return samples;
    }

    private static double Median(IReadOnlyList<double> samples)
    {
        var sorted = samples.OrderBy(static value => value).ToArray();
        return sorted[sorted.Length / 2];
    }

    private static double Percentile(IReadOnlyList<double> samples, double fraction)
    {
        var sorted = samples.OrderBy(static value => value).ToArray();
        var index = (int)Math.Min(sorted.Length - 1, Math.Floor(fraction * sorted.Length));
        return sorted[index];
    }

    private static void Report(string label, IReadOnlyList<double> samples)
    {
        if (samples.Count == 0)
        {
            return;
        }

        Console.WriteLine(
            $"{label}: медиана {Median(samples):F1} мс, " +
            $"p90 {Percentile(samples, 0.90):F1} мс, " +
            $"максимум {samples.Max():F1} мс");
    }

    /// <summary>
    /// Частота, при которой разбор занимает не более половины одного ядра. Запас
    /// нужен потому, что распознавание делит машину с игрой, а не владеет ею.
    /// </summary>
    private static void PrintAffordableRate(string label, IReadOnlyList<double> samples)
    {
        var p90 = Percentile(samples, 0.90);
        Console.WriteLine(
            $"{label}: по p90 {p90:F1} мс одно ядро тянет {1000 / p90:F0} кадр/с, " +
            $"половина ядра — {500 / p90:F0} кадр/с");
    }
}
