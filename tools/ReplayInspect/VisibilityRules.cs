using TempoCue.Application;
using TempoCue.Vision;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Сравнивает правила, по которым распознанный участник получает право стать
/// видимым.
///
/// Сейчас уверенность наблюдения — среднее сходства с портретом и уверенности
/// цветного контура. Усреднение появилось раньше отклонения по отрицательным
/// эталонам и было тогда единственной защитой от башен и миньонов. Вопрос замера:
/// осталось ли оно нужным теперь, когда статика отсеивается явно, и сколько
/// участников оно стоит.
///
/// Ложные видимые считаются отдельно: подтверждение, не совпавшее ни с одним
/// размеченным чемпионом, но прошедшее порог, — это ровно то, чего усреднение
/// должно не пускать.
/// </summary>
internal static class VisibilityRules
{
    private static readonly (string Name, Func<MinimapChampionMarkerCandidate, double> Rule)[]
        Rules =
        [
            ("среднее (сейчас)", static candidate => candidate.Confidence),
            ("только портрет", static candidate => candidate.IdentityConfidence),
            (
                "портрет 0,75 + контур 0,25",
                static candidate =>
                    (candidate.IdentityConfidence * 0.75) +
                    (ContourConfidence(candidate) * 0.25)),
            (
                "максимум из двух",
                static candidate => Math.Max(
                    candidate.Confidence,
                    candidate.IdentityConfidence))
        ];

    public static async Task<int> RunAsync(string corpusDirectory)
    {
        var threshold = ParticipantVisibilityTracker.DefaultMinimumObservationConfidence;
        var runner = new MinimapReplayRunner();
        var samples = new List<(string Name, MinimapReplayOutcome Outcome, MinimapReplayEvaluation Evaluation)>();

        foreach (var path in ReplaySampleFile.EnumerateCorpus(corpusDirectory))
        {
            if (await ReplaySampleFile.TryLoadLabelsAsync(path) is not { } truth)
            {
                continue;
            }

            var outcome = runner.Run(await ReplaySampleFile.LoadAsync(path)).Actual;
            samples.Add((
                ReplaySampleFile.Name(path),
                outcome,
                MinimapReplayEvaluator.Evaluate(outcome, truth)));
        }

        var labelled = samples.Sum(sample =>
            sample.Evaluation.ConfirmedChampions.Count + sample.Evaluation.MissedChampions.Count);
        Console.WriteLine($"Кадров: {samples.Count}, размеченных чемпионов: {labelled}");
        Console.WriteLine($"Порог уверенности наблюдения: {threshold:F2}");
        Console.WriteLine();
        Console.WriteLine("правило | видимых | ложных видимых | худший видимый | лучший невидимый");

        foreach (var (name, rule) in Rules)
        {
            var visible = 0;
            var falseVisible = 0;
            var worstVisible = double.MaxValue;
            var bestHidden = double.MinValue;

            foreach (var sample in samples)
            {
                foreach (var champion in sample.Evaluation.ConfirmedChampions)
                {
                    var score = rule(champion.Candidate);
                    if (score >= threshold)
                    {
                        visible++;
                        worstVisible = Math.Min(worstVisible, score);
                    }
                    else
                    {
                        bestHidden = Math.Max(bestHidden, score);
                    }
                }

                // Подтверждение без размеченного чемпиона — это башня, миньоны или
                // другой статичный объект. Если оно проходит порог, правило пускает
                // на карту то, чего там нет.
                foreach (var falsePositive in sample.Evaluation.FalsePositives)
                {
                    if (rule(falsePositive.Candidate) >= threshold)
                    {
                        falseVisible++;
                    }
                }
            }

            Console.WriteLine(
                $"{name,-26} | {visible,7} | {falseVisible,14} | " +
                $"{(worstVisible == double.MaxValue ? 0 : worstVisible),14:F3} | " +
                $"{(bestHidden == double.MinValue ? 0 : bestHidden),16:F3}");
        }

        Console.WriteLine();
        Console.WriteLine(
            "Ложные видимые считаются по подтверждениям вне разметки. Их сейчас в " +
            "корпусе ноль при любом правиле, поэтому корпус не различает правила по " +
            "безопасности — только по потерям. Это ограничение выборки, а не " +
            "доказательство безопасности.");
        return 0;
    }

    /// <summary>
    /// Восстанавливает уверенность контура из среднего: она в кандидате не
    /// сохраняется отдельно, а нужна, чтобы взвесить слагаемые иначе.
    /// </summary>
    private static double ContourConfidence(MinimapChampionMarkerCandidate candidate) =>
        Math.Clamp((candidate.Confidence * 2) - candidate.IdentityConfidence, 0, 1);
}
