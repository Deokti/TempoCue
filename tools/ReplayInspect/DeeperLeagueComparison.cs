using System.Diagnostics;
using TempoCue.Vision;
using TempoCue.Vision.DeeperLeague;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Прогоняет по одному корпусу основной распознаватель и стороннюю модель
/// DeeperLeague, сравнивая обоих с одной и той же ручной разметкой.
/// </summary>
internal static class DeeperLeagueComparison
{
    public static async Task<int> RunAsync(string corpusDirectory)
    {
        if (FindModelDirectory() is not { } modelDirectory)
        {
            Console.Error.WriteLine(
                "Не найден каталог models/deeperleague с файлами best.onnx и champMap.json.");
            return 1;
        }

        using var model = new DeeperLeagueMinimapRecognizer(
            Path.Combine(modelDirectory, "best.onnx"),
            Path.Combine(modelDirectory, "champMap.json"));
        var runner = new MinimapReplayRunner();
        var ours = new Totals();
        var theirs = new Totals();
        var elapsed = new List<double>();

        foreach (var path in ReplaySampleFile.EnumerateCorpus(corpusDirectory))
        {
            if (await ReplaySampleFile.TryLoadLabelsAsync(path) is not { } truth)
            {
                continue;
            }

            var sample = await ReplaySampleFile.LoadAsync(path);
            var unsupported = FindUnsupportedChampions(sample, truth);

            var ourOutcome = runner.Run(sample).Actual;
            var stopwatch = Stopwatch.StartNew();
            var theirCandidates = model.Recognize(
                sample.BgraPixels.Span,
                sample.ImageWidth,
                sample.ImageHeight,
                sample.ImageStride,
                sample.ChampionReferences);
            stopwatch.Stop();
            elapsed.Add(stopwatch.Elapsed.TotalMilliseconds);

            var theirOutcome = new MinimapReplayOutcome(
                [],
                theirCandidates.Length,
                theirCandidates,
                theirCandidates.Length == 0
                    ? 0
                    : theirCandidates.Max(static candidate => candidate.IdentityConfidence));

            var ourEvaluation = MinimapReplayEvaluator.Evaluate(ourOutcome, truth);
            var theirEvaluation = MinimapReplayEvaluator.Evaluate(theirOutcome, truth);
            ours.Add(ourEvaluation);
            theirs.Add(theirEvaluation);

            Console.WriteLine($"=== {ReplaySampleFile.Name(path)} ===");
            if (unsupported.Count > 0)
            {
                Console.WriteLine(
                    "  ВНИМАНИЕ: модель не знает чемпионов — " +
                    string.Join(", ", unsupported) +
                    "; кадр непригоден для честного сравнения");
            }

            Console.WriteLine(
                $"  наш:          верных {ourEvaluation.ConfirmedChampions.Count}, " +
                $"пропущено {ourEvaluation.MissedChampions.Count}, " +
                $"ложных {ourEvaluation.FalsePositives.Count}");
            Console.WriteLine(
                $"  DeeperLeague: верных {theirEvaluation.ConfirmedChampions.Count}, " +
                $"пропущено {theirEvaluation.MissedChampions.Count}, " +
                $"ложных {theirEvaluation.FalsePositives.Count}, " +
                $"{stopwatch.Elapsed.TotalMilliseconds:F0} мс");

            foreach (var recovered in theirEvaluation.ConfirmedChampions
                .Where(confirmed => ourEvaluation.MissedChampions.Any(missed =>
                    string.Equals(
                        missed.ChampionId,
                        confirmed.Candidate.ChampionId,
                        StringComparison.OrdinalIgnoreCase))))
            {
                Console.WriteLine(
                    $"    + восстановлен наш пропуск: {recovered.Candidate.ChampionId} " +
                    $"({recovered.Candidate.IdentityConfidence:F2})");
            }

            foreach (var falsePositive in theirEvaluation.FalsePositives)
            {
                Console.WriteLine(
                    $"    - ложное подтверждение: {falsePositive.Candidate.ChampionId} " +
                    $"в ({falsePositive.Candidate.X},{falsePositive.Candidate.Y}) " +
                    $"{falsePositive.NearestRegion?.Kind.ToString() ?? "вне разметки"}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== ИТОГО ПО КОРПУСУ ===");
        Console.WriteLine($"  наш:          {ours}");
        Console.WriteLine($"  DeeperLeague: {theirs}");
        if (elapsed.Count > 0)
        {
            var sorted = elapsed.OrderBy(static value => value).ToArray();
            Console.WriteLine(
                $"  время инференса: медиана {sorted[sorted.Length / 2]:F0} мс, " +
                $"максимум {sorted[^1]:F0} мс");
        }

        return 0;
    }

    /// <summary>
    /// Модель обучена по ростеру патча 13.14. Кадр, где есть чемпион новее, для
    /// сравнения непригоден: отсутствие класса приводит либо к пропуску, либо к
    /// уверенной ошибке, и то и другое искажает итог.
    /// </summary>
    private static List<string> FindUnsupportedChampions(
        MinimapReplaySample sample,
        MinimapReplayGroundTruth truth)
    {
        var known = DeeperLeagueChampionMap.Load(
            Path.Combine(FindModelDirectory()!, "champMap.json"));
        var knownIds = Enumerable.Range(0, known.Count)
            .Select(index => known[index])
            .Where(static id => id is not null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        return sample.ChampionReferences
            .Select(static reference => reference.ChampionId)
            .Concat(truth.Regions
                .Where(static region => region.Kind == MinimapGroundTruthKind.Champion)
                .Select(static region => region.ChampionId!))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(id => !knownIds.Contains(id))
            .OrderBy(static id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? FindModelDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "models", "deeperleague");
            if (File.Exists(Path.Combine(candidate, "best.onnx")))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private sealed class Totals
    {
        private int _confirmed;
        private int _missed;
        private int _falsePositives;

        public void Add(MinimapReplayEvaluation evaluation)
        {
            _confirmed += evaluation.ConfirmedChampions.Count;
            _missed += evaluation.MissedChampions.Count;
            _falsePositives += evaluation.FalsePositives.Count;
        }

        public override string ToString() =>
            $"верных {_confirmed}, пропущено {_missed}, ложных {_falsePositives}";
    }
}
