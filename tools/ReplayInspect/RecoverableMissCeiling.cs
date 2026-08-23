using TempoCue.Vision;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Считает потолок выигрыша от детектора, отвечающего только на вопрос «где».
///
/// Каждый пропуск конвейера относится к одному из двух классов. Если в месте
/// пропущенного чемпиона не родилась область сравнения, значит его не увидел
/// генератор областей — такой пропуск детектор восстановит. Если область есть, а
/// участник не подтверждён, дело в сравнении портретов, и детектор здесь не
/// поможет: он вернёт ту же область, которую мы уже отвергли.
///
/// Сумма первого класса и есть весь возможный выигрыш от собственной модели в
/// варианте «сеть находит, эталоны опознают».
/// </summary>
internal static class RecoverableMissCeiling
{
    public static async Task<int> RunAsync(string corpusDirectory)
    {
        var runner = new MinimapReplayRunner();
        var missingRegion = new List<string>();
        var wrongIdentity = new List<string>();
        var confirmed = 0;
        var strongConfirmations = 0;
        var weakConfirmations = new List<string>();
        var visibilityThreshold = TempoCue.Application.ParticipantVisibilityTracker
            .DefaultMinimumObservationConfidence;

        foreach (var samplePath in ReplaySampleFile.EnumerateCorpus(corpusDirectory))
        {
            if (await ReplaySampleFile.TryLoadLabelsAsync(samplePath) is not { } truth)
            {
                continue;
            }

            var sample = await ReplaySampleFile.LoadAsync(samplePath);
            var outcome = runner.Run(sample).Actual;
            var evaluation = MinimapReplayEvaluator.Evaluate(outcome, truth);
            var diagnostics = runner.RunRegionDiagnostics(sample);
            confirmed += evaluation.ConfirmedChampions.Count;

            var name = ReplaySampleFile.Name(samplePath);
            foreach (var champion in evaluation.ConfirmedChampions)
            {
                if (champion.Candidate.Confidence >= visibilityThreshold)
                {
                    strongConfirmations++;
                    continue;
                }

                weakConfirmations.Add(
                    $"{name}: {champion.Candidate.ChampionId} — сходство " +
                    $"{champion.Candidate.IdentityConfidence:F3}, уверенность " +
                    $"{champion.Candidate.Confidence:F3}");
            }

            foreach (var missed in evaluation.MissedChampions)
            {
                var nearest = FindNearestProposal(diagnostics, missed);
                if (nearest is null)
                {
                    missingRegion.Add(
                        $"{name}: {missed.ChampionId} в ({missed.CenterX:F0},{missed.CenterY:F0}) " +
                        "— области сравнения нет");
                    continue;
                }

                var scores = nearest.ReferenceSimilarities
                    .OrderByDescending(similarity => similarity.Similarity)
                    .ToArray();
                var own = scores.FirstOrDefault(similarity => string.Equals(
                    similarity.ChampionId,
                    missed.ChampionId,
                    StringComparison.OrdinalIgnoreCase));
                wrongIdentity.Add(
                    $"{name}: {missed.ChampionId} в ({missed.CenterX:F0},{missed.CenterY:F0}) " +
                    $"— область есть, свой балл {own?.Similarity ?? double.NaN:F3}, " +
                    $"лучший {scores[0].ChampionId} {scores[0].Similarity:F3}, " +
                    $"статика {nearest.NegativeSimilarity:F3}");
            }
        }

        Console.WriteLine("=== ПРОПУСКИ БЕЗ ОБЛАСТИ СРАВНЕНИЯ (восстановимы детектором) ===");
        foreach (var line in missingRegion)
        {
            Console.WriteLine($"  {line}");
        }

        Console.WriteLine();
        Console.WriteLine("=== ПРОПУСКИ С ОБЛАСТЬЮ (детектор не поможет) ===");
        foreach (var line in wrongIdentity)
        {
            Console.WriteLine($"  {line}");
        }

        var total = confirmed + missingRegion.Count + wrongIdentity.Count;
        Console.WriteLine();
        Console.WriteLine("=== ИТОГО ===");
        Console.WriteLine($"  размеченных чемпионов: {total}");
        Console.WriteLine($"  подтверждено: {confirmed}");
        Console.WriteLine(
            $"  потолок детектора: {missingRegion.Count} " +
            $"({(total == 0 ? 0 : missingRegion.Count * 100d / total):F1} % от всех)");
        Console.WriteLine(
            $"  вне досягаемости детектора: {wrongIdentity.Count} " +
            $"({(total == 0 ? 0 : wrongIdentity.Count * 100d / total):F1} % от всех)");

        // Корпус проверяет распознавание, но не проверяет, доходит ли найденный
        // участник до состояния «виден». Между ними стоит порог уверенности
        // наблюдения, и подтверждённый чемпион с баллом ниже него игроку не
        // показывается вовсе.
        Console.WriteLine();
        Console.WriteLine("=== ДОХОДЯТ ЛИ ПОДТВЕРЖДЁННЫЕ ДО ВИДИМОСТИ ===");
        Console.WriteLine($"  порог уверенности наблюдения: {visibilityThreshold:F2}");
        Console.WriteLine(
            $"  проходят порог: {strongConfirmations} из {confirmed}");
        Console.WriteLine(
            $"  найдены, но не показываются: {confirmed - strongConfirmations}");
        foreach (var line in weakConfirmations)
        {
            Console.WriteLine($"    {line}");
        }

        return 0;
    }

    /// <summary>
    /// Область считается относящейся к пропуску, если её центр ближе, чем половина
    /// стороны значка. Тот же порог, по которому измеритель сопоставляет
    /// подтверждения с разметкой.
    /// </summary>
    private static MinimapRegionDiagnostics? FindNearestProposal(
        IReadOnlyList<MinimapRegionDiagnostics> diagnostics,
        MinimapGroundTruthRegion region)
    {
        MinimapRegionDiagnostics? nearest = null;
        var bestDistance = double.MaxValue;
        foreach (var candidate in diagnostics)
        {
            var deltaX = candidate.Proposal.X + (candidate.Proposal.Width / 2d) - region.CenterX;
            var deltaY = candidate.Proposal.Y + (candidate.Proposal.Height / 2d) - region.CenterY;
            var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
            if (distance <= MinimapReplayEvaluator.MatchCenterDistance && distance < bestDistance)
            {
                bestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }
}
