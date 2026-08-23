using TempoCue.Vision;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Отвечает на вопрос, помогает ли перебор пяти масштабов или мешает.
///
/// Максимум по масштабам берётся отдельно для каждого эталона, а затем эталоны
/// сравниваются между собой. Если выбор масштаба — шум, то каждый эталон получает
/// случайную прибавку, все баллы поднимаются, а разница между ними сжимается.
/// Тогда перебор вредит различению, хотя абсолютные баллы выглядят выше.
///
/// Поэтому сравнивается не балл лидера, а **отрыв лидера от второго**: именно он
/// решает, будет ли участник подтверждён (`MinimumIdentityMargin`). Заодно
/// считается результат по разметке — полнота важнее любой промежуточной величины.
/// </summary>
internal static class ScaleSweep
{
    public static async Task<int> RunAsync(string corpusDirectory)
    {
        var variants = new (string Name, int[]? Sides)[]
        {
            ("все пять (штатный)", null),
            ("только 13", [13]),
            ("только 15", [15]),
            ("только 17", [17]),
            ("только 19", [19]),
            ("только 21", [21]),
            ("без 13: 15-21", [15, 17, 19, 21]),
            ("без 13 и 15: 17-21", [17, 19, 21]),
            ("17 и 21", [17, 21]),
            ("19 и 21", [19, 21]),
            ("15, 18, 21", [15, 18, 21]),
            ("13, 17, 21", [13, 17, 21])
        };

        var samples = new List<(MinimapReplaySample Sample, MinimapReplayGroundTruth Truth)>();
        foreach (var path in ReplaySampleFile.EnumerateCorpus(corpusDirectory))
        {
            if (await ReplaySampleFile.TryLoadLabelsAsync(path) is { } truth)
            {
                samples.Add((await ReplaySampleFile.LoadAsync(path), truth));
            }
        }

        Console.WriteLine($"Кадров с разметкой: {samples.Count}");
        Console.WriteLine();
        Console.WriteLine(
            "набор масштабов | верных | пропущено | ложных | отрыв: медиана | p10 | " +
            "балл лидера | кадр");

        foreach (var (name, sides) in variants)
        {
            var runner = new MinimapReplayRunner(
                MinimapReplayRunner.DefaultDegreeOfParallelism,
                sides);
            var confirmed = 0;
            var missed = 0;
            var falsePositives = 0;
            var margins = new List<double>();
            var leaders = new List<double>();
            var durations = new List<double>();

            foreach (var (sample, truth) in samples)
            {
                runner.Run(sample);
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var outcome = runner.Run(sample).Actual;
                stopwatch.Stop();
                durations.Add(stopwatch.Elapsed.TotalMilliseconds);
                var evaluation = MinimapReplayEvaluator.Evaluate(outcome, truth);
                confirmed += evaluation.ConfirmedChampions.Count;
                missed += evaluation.MissedChampions.Count;
                falsePositives += evaluation.FalsePositives.Count;

                foreach (var diagnostics in runner.RunRegionDiagnostics(sample))
                {
                    var ordered = diagnostics.ReferenceSimilarities
                        .OrderByDescending(similarity => similarity.Similarity)
                        .ToArray();
                    if (ordered.Length < 2)
                    {
                        continue;
                    }

                    leaders.Add(ordered[0].Similarity);
                    margins.Add(ordered[0].Similarity - ordered[1].Similarity);
                }
            }

            Console.WriteLine(
                $"{name,-18} | {confirmed,6} | {missed,9} | {falsePositives,6} | " +
                $"{Median(margins),14:F4} | {Percentile(margins, 0.10),6:F4} | " +
                $"{Median(leaders),11:F3} | {Median(durations),7:F0} мс");
        }

        Console.WriteLine();
        Console.WriteLine(
            "Отрыв важнее балла лидера: подтверждение требует превышения " +
            $"0,04. " +
            "p10 показывает трудные области, где решение и срывается.");
        return 0;
    }

    private static double Median(IReadOnlyList<double> values) =>
        Percentile(values, 0.50);

    private static double Percentile(IReadOnlyList<double> values, double fraction)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.OrderBy(static value => value).ToArray();
        var index = (int)Math.Min(sorted.Length - 1, Math.Floor(fraction * sorted.Length));
        return sorted[index];
    }
}
