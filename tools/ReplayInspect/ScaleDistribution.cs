using TempoCue.Vision;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Считает, на каком масштабе шаблона берётся лучший балл.
///
/// Перебор пяти масштабов страхует не от настроек игрока — размер значка на
/// каноническом кадре постоянен, — а от неточности определения границ миникарты.
/// Если побеждает почти всегда один и тот же масштаб, перебор можно сузить, и
/// освободившееся потратить на более подробное сравнение: сейчас личность
/// определяется примерно по сотне пикселей центрального выреза `11 x 11`.
///
/// Отдельно считается распределение для победителя области — именно оно решает,
/// кем окажется участник, — и для всех эталонов подряд.
/// </summary>
internal static class ScaleDistribution
{
    public static async Task<int> RunAsync(string corpusDirectory)
    {
        var runner = new MinimapReplayRunner();
        var winnerCounts = new Dictionary<int, int>();
        var allCounts = new Dictionary<int, int>();
        var regions = 0;

        foreach (var samplePath in ReplaySampleFile.EnumerateCorpus(corpusDirectory))
        {
            var sample = await ReplaySampleFile.LoadAsync(samplePath);
            foreach (var diagnostics in runner.RunRegionDiagnostics(sample))
            {
                if (diagnostics.ReferenceSimilarities.Count == 0)
                {
                    continue;
                }

                regions++;
                var best = diagnostics.ReferenceSimilarities
                    .OrderByDescending(similarity => similarity.Similarity)
                    .First();
                Increment(winnerCounts, best.ScaleIndex);
                foreach (var similarity in diagnostics.ReferenceSimilarities)
                {
                    Increment(allCounts, similarity.ScaleIndex);
                }
            }
        }

        // Стороны области в исходном кадре, из которых строятся шаблоны.
        var sourceSides = new[] { 13, 15, 17, 19, 21 };
        Console.WriteLine($"Областей сравнения: {regions}");
        Console.WriteLine();
        Console.WriteLine("масштаб | сторона | победитель области | все эталоны");
        for (var index = 0; index < sourceSides.Length; index++)
        {
            var winner = winnerCounts.GetValueOrDefault(index);
            var all = allCounts.GetValueOrDefault(index);
            var winnerShare = regions == 0 ? 0 : winner * 100d / regions;
            var allTotal = allCounts.Values.Sum();
            var allShare = allTotal == 0 ? 0 : all * 100d / allTotal;
            Console.WriteLine(
                $"{index,7} | {sourceSides[index],7} | {winner,7} ({winnerShare,5:F1} %) | " +
                $"{all,6} ({allShare,5:F1} %)");
        }

        Console.WriteLine();
        var dominant = winnerCounts.Count == 0
            ? -1
            : winnerCounts.OrderByDescending(pair => pair.Value).First().Key;
        var dominantShare = regions == 0
            ? 0
            : winnerCounts.GetValueOrDefault(dominant) * 100d / regions;
        Console.WriteLine(
            $"Чаще всего побеждает масштаб {dominant} (сторона {sourceSides[dominant]}): " +
            $"{dominantShare:F1} % областей.");
        Console.WriteLine(
            "Если доля близка к ста процентам, перебор можно сузить; если победители " +
            "размазаны, сначала нужно чинить определение границ миникарты.");
        return 0;
    }

    private static void Increment(Dictionary<int, int> counts, int key) =>
        counts[key] = counts.GetValueOrDefault(key) + 1;
}
