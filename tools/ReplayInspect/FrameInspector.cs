using TempoCue.Vision;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>Показывает, что распозналось в отдельном кадре и почему.</summary>
internal static class FrameInspector
{
    public static async Task<int> PrintRecognitionAsync(IEnumerable<string> paths)
    {
        var runner = new MinimapReplayRunner();
        foreach (var path in paths)
        {
            var sample = await ReplaySampleFile.LoadAsync(path);
            var outcome = runner.Run(sample).Actual;
            Console.WriteLine($"=== {ReplaySampleFile.Name(path)} ===");
            Console.WriteLine($"снят: {sample.CapturedAt:O}");
            Console.WriteLine($"эталоны состава ({sample.ChampionReferences.Count}):");
            foreach (var reference in sample.ChampionReferences)
            {
                Console.WriteLine($"  {reference.ChampionId} [{reference.Affiliation}]");
            }

            Console.WriteLine(
                $"цветных контуров: {outcome.MarkerCandidates.Length}, " +
                $"областей сравнения: {outcome.ChampionProposalCount}, " +
                $"лучший балл: {outcome.MaximumIdentityConfidence:F3}");
            Console.WriteLine($"подтверждено ({outcome.ChampionCandidates.Length}):");
            foreach (var candidate in outcome.ChampionCandidates)
            {
                Console.WriteLine(
                    $"  {candidate.ChampionId} [{candidate.Affiliation}] " +
                    $"в ({candidate.X},{candidate.Y}) " +
                    $"сходство={candidate.IdentityConfidence:F3} " +
                    $"контур={candidate.ContourAffiliation}");
            }

            if (await ReplaySampleFile.TryLoadLabelsAsync(path) is { } truth)
            {
                var evaluation = MinimapReplayEvaluator.Evaluate(outcome, truth);
                Console.WriteLine(
                    $"против разметки: верных {evaluation.ConfirmedChampions.Count}, " +
                    $"пропущено {evaluation.MissedChampions.Count} " +
                    $"(допуск {truth.AllowedMissedChampions}), " +
                    $"ложных {evaluation.FalsePositives.Count} " +
                    $"(допуск {truth.AllowedFalsePositives})");
                foreach (var missed in evaluation.MissedChampions)
                {
                    Console.WriteLine(
                        $"  пропущен {missed.ChampionId} в " +
                        $"({missed.X},{missed.Y})");
                }
            }

            Console.WriteLine();
        }

        return 0;
    }

    public static async Task<int> PrintRegionDiagnosticsAsync(IEnumerable<string> paths)
    {
        var runner = new MinimapReplayRunner();
        foreach (var path in paths)
        {
            var sample = await ReplaySampleFile.LoadAsync(path);
            Console.WriteLine($"=== {ReplaySampleFile.Name(path)} ===");
            foreach (var region in runner.RunRegionDiagnostics(sample))
            {
                Console.WriteLine(Describe(region));
            }

            Console.WriteLine();
        }

        return 0;
    }

    public static async Task<int> ProbePointsAsync(string path, IEnumerable<string> coordinates)
    {
        var points = coordinates.ToArray();
        if (points.Length % 2 != 0)
        {
            Console.Error.WriteLine("Координаты задаются парами x и y.");
            return 1;
        }

        var sample = await ReplaySampleFile.LoadAsync(path);
        var runner = new MinimapReplayRunner();
        Console.WriteLine($"=== {ReplaySampleFile.Name(path)} ===");
        for (var index = 0; index + 1 < points.Length; index += 2)
        {
            if (!int.TryParse(points[index], out var x) ||
                !int.TryParse(points[index + 1], out var y))
            {
                Console.Error.WriteLine(
                    $"Не удалось разобрать координаты: {points[index]} {points[index + 1]}");
                return 1;
            }

            Console.WriteLine(Describe(runner.ProbeRegion(sample, x, y), topCount: 4));
        }

        return 0;
    }

    /// <summary>
    /// Прогон с единственным эталоном показывает, где участник распознаётся без
    /// конкуренции состава. Отделяет слабое сходство от проигрыша соседу.
    /// </summary>
    public static async Task<int> PrintSoloRecognitionAsync(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            var sample = await ReplaySampleFile.LoadAsync(path);
            Console.WriteLine($"=== {ReplaySampleFile.Name(path)} ===");
            foreach (var reference in sample.ChampionReferences)
            {
                var single = new MinimapReplaySample(
                    sample.CapturedAt,
                    sample.ImageWidth,
                    sample.ImageHeight,
                    sample.ImageStride,
                    sample.BgraPixels.Span,
                    [reference],
                    sample.Baseline);
                var outcome = new MinimapReplayRunner().Run(single).Actual;
                var hits = string.Join(
                    "; ",
                    outcome.ChampionCandidates.Select(candidate =>
                        $"({candidate.X},{candidate.Y}) сходство={candidate.IdentityConfidence:F3}"));
                Console.WriteLine(
                    $"  {reference.ChampionId} [{reference.Affiliation}]: " +
                    (hits.Length == 0 ? "не подтверждён" : hits) +
                    $" (лучший наблюдённый {outcome.MaximumIdentityConfidence:F3})");
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static string Describe(MinimapRegionDiagnostics region, int topCount = 3)
    {
        var top = region.ReferenceSimilarities
            .Take(topCount)
            .Select(similarity =>
                $"{similarity.ChampionId}[{similarity.Affiliation.ToString()[0]}]=" +
                $"{similarity.Similarity:F3}");
        var ring = region.RingStats;
        return
            $"  ({region.Proposal.X},{region.Proposal.Y}) " +
            $"контур={region.Proposal.Affiliation} " +
            $"негативы={region.NegativeSimilarity:F3} | {string.Join(" ", top)} | " +
            $"кольцо: враг={ring.EnemyColored} союзник={ring.AllyColored} " +
            $"ярких={ring.BrightPixels}";
    }
}
