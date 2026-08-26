using System.Diagnostics;
using TempoCue.Vision;
using TempoCue.Vision.OwnModel;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Прогоняет по одному корпусу штатный распознаватель и свою модель, сравнивая
/// обоих с одной и той же ручной разметкой.
/// </summary>
/// <remarks>
/// Числа этого режима — то, ради чего модель переносилась в приложение. До него
/// четыре пункта договора о приёмке измерить было нечем, а полнота мерилась
/// своей линейкой в модельном репозитории: там допуск совпадения центра стоял
/// 3 пикселя против 13 в <see cref="MinimapReplayEvaluator"/>, и расхождение
/// заметили только по итогам сравнения.
///
/// Первое, что надо проверить на этом режиме, — совпадение с образцом
/// <c>tools/run_onnx.py</c> модельного репозитория. Он считает те же кадры теми
/// же весами, и расхождение означает ошибку порта.
/// </remarks>
internal static class OwnModelComparison
{
    public static async Task<int> RunAsync(string corpusDirectory)
    {
        if (OwnModelFrameAnalyzer.FindModelDirectory() is not { } modelDirectory)
        {
            Console.Error.WriteLine(
                "Не найден каталог models/own-model с файлами model.json, " +
                "detector.onnx и identity.onnx. Их отдаёт tools/export_onnx.py " +
                "модельного репозитория.");
            return 1;
        }

        using var model = new OwnModelMinimapRecognizer(modelDirectory);
        Console.WriteLine($"Своя модель: {modelDirectory}");
        Console.WriteLine(
            $"  классов {model.Setup.Vocabulary.Count}, " +
            $"порог вершины {model.Setup.PeakThreshold:F2}, " +
            $"кадр {model.Setup.FrameSide}, шаг {model.Setup.Stride}");
        Console.WriteLine(
            $"  допуск совпадения центра при оценке: " +
            $"{MinimapReplayEvaluator.MatchCenterDistance} px");
        Console.WriteLine();

        var runner = new MinimapReplayRunner();
        var ours = new Totals();
        var modelTotals = new Totals();
        var ourElapsed = new List<double>();
        var modelElapsed = new List<double>();

        foreach (var path in ReplaySampleFile.EnumerateCorpus(corpusDirectory))
        {
            if (await ReplaySampleFile.TryLoadLabelsAsync(path) is not { } truth)
            {
                continue;
            }

            var sample = await ReplaySampleFile.LoadAsync(path);

            var ourStopwatch = Stopwatch.StartNew();
            var ourOutcome = runner.Run(sample).Actual;
            ourStopwatch.Stop();
            ourElapsed.Add(ourStopwatch.Elapsed.TotalMilliseconds);

            var stopwatch = Stopwatch.StartNew();
            var candidates = model.Recognize(
                sample.BgraPixels.Span,
                sample.ImageWidth,
                sample.ImageHeight,
                sample.ImageStride,
                sample.ChampionReferences);
            stopwatch.Stop();
            modelElapsed.Add(stopwatch.Elapsed.TotalMilliseconds);

            var modelOutcome = new MinimapReplayOutcome(
                [],
                candidates.Length,
                candidates,
                candidates.Length == 0
                    ? 0
                    : candidates.Max(static candidate => candidate.IdentityConfidence));

            var ourEvaluation = MinimapReplayEvaluator.Evaluate(ourOutcome, truth);
            var modelEvaluation = MinimapReplayEvaluator.Evaluate(modelOutcome, truth);
            ours.Add(ourEvaluation);
            modelTotals.Add(modelEvaluation);

            Console.WriteLine($"=== {ReplaySampleFile.Name(path)} ===");
            Console.WriteLine(
                $"  конвейер:   верных {ourEvaluation.ConfirmedChampions.Count}, " +
                $"пропущено {ourEvaluation.MissedChampions.Count}, " +
                $"ложных {ourEvaluation.FalsePositives.Count}, " +
                $"{ourStopwatch.Elapsed.TotalMilliseconds:F0} мс");
            Console.WriteLine(
                $"  своя модель: верных {modelEvaluation.ConfirmedChampions.Count}, " +
                $"пропущено {modelEvaluation.MissedChampions.Count}, " +
                $"ложных {modelEvaluation.FalsePositives.Count}, " +
                $"{stopwatch.Elapsed.TotalMilliseconds:F0} мс");

            // Пункт 4 договора о приёмке: замена, которая берёт новых и теряет
            // старых, — не улучшение. Поэтому потери показываются поимённо, а не
            // растворяются в сумме.
            foreach (var lost in modelEvaluation.MissedChampions
                .Where(missed => ourEvaluation.ConfirmedChampions.Any(confirmed =>
                    string.Equals(
                        confirmed.Candidate.ChampionId,
                        missed.ChampionId,
                        StringComparison.OrdinalIgnoreCase))))
            {
                Console.WriteLine($"    - ПОТЕРЯН против конвейера: {lost.ChampionId}");
            }

            foreach (var recovered in modelEvaluation.ConfirmedChampions
                .Where(confirmed => ourEvaluation.MissedChampions.Any(missed =>
                    string.Equals(
                        missed.ChampionId,
                        confirmed.Candidate.ChampionId,
                        StringComparison.OrdinalIgnoreCase))))
            {
                Console.WriteLine(
                    $"    + восстановлен пропуск конвейера: {recovered.Candidate.ChampionId} " +
                    $"({recovered.Candidate.IdentityConfidence:F2})");
            }

            foreach (var falsePositive in modelEvaluation.FalsePositives)
            {
                Console.WriteLine(
                    $"    - ложное подтверждение: {falsePositive.Candidate.ChampionId} " +
                    $"в ({falsePositive.Candidate.X},{falsePositive.Candidate.Y}) " +
                    $"{falsePositive.NearestRegion?.Kind.ToString() ?? "вне разметки"}");
            }
        }

        Console.WriteLine();
        Console.WriteLine("=== ИТОГО ПО КОРПУСУ ===");
        Console.WriteLine($"  конвейер:    {ours}");
        Console.WriteLine($"  своя модель: {modelTotals}");
        PrintTiming("конвейера", ourElapsed);
        PrintTiming("своей модели", modelElapsed);
        Console.WriteLine();
        Console.WriteLine(
            "Пункт 3 договора: не дороже 125 мс при двух потоках. Пункт 1: ложных ноль.");

        return 0;
    }

    private static void PrintTiming(string label, List<double> samples)
    {
        if (samples.Count == 0)
        {
            return;
        }

        var sorted = samples.OrderBy(static value => value).ToArray();
        Console.WriteLine(
            $"  время {label}: медиана {sorted[sorted.Length / 2]:F1} мс, " +
            $"максимум {sorted[^1]:F1} мс");
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
