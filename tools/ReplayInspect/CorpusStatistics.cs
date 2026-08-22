using TempoCue.Vision;

namespace TempoCue.Tools.ReplayInspect;

/// <summary>
/// Считает по всему корпусу величины, которые нельзя увидеть на одном кадре:
/// базовый уровень эталонов и запас настоящих портретов над отрицательными
/// эталонами. Эти числа обосновывают пороги вместо подбора по наблюдению.
/// </summary>
internal static class CorpusStatistics
{
    private const int CanvasSide = 64;

    public static async Task<int> PrintReferenceBaselinesAsync(string corpusDirectory)
    {
        var rows = new List<ScoreRow>();
        var runner = new MinimapReplayRunner();
        foreach (var path in ReplaySampleFile.EnumerateCorpus(corpusDirectory))
        {
            if (await ReplaySampleFile.TryLoadLabelsAsync(path) is not { } truth)
            {
                continue;
            }

            var sample = await ReplaySampleFile.LoadAsync(path);
            var frame = ReplaySampleFile.Name(path);
            foreach (var region in truth.Regions)
            {
                if (ReplaySampleFile.ToProbeOrigin(region) is not var (x, y))
                {
                    continue;
                }

                foreach (var similarity in runner.ProbeRegion(sample, x, y).ReferenceSimilarities)
                {
                    var isOwn = region.Kind == MinimapGroundTruthKind.Champion &&
                        string.Equals(
                            similarity.ChampionId,
                            region.ChampionId,
                            StringComparison.OrdinalIgnoreCase);
                    rows.Add(new ScoreRow(
                        frame,
                        similarity.ChampionId,
                        similarity.Similarity,
                        isOwn,
                        region.Kind == MinimapGroundTruthKind.Champion
                            ? region.ChampionId ?? "?"
                            : region.Kind.ToString()));
                }
            }
        }

        if (rows.Count == 0)
        {
            Console.Error.WriteLine("В каталоге нет размеченных кадров.");
            return 1;
        }

        Console.WriteLine("=== БАЗОВЫЙ УРОВЕНЬ ЭТАЛОНОВ ===");
        Console.WriteLine(
            "эталон           | своих | мин.свой | чужих | ср.чужой | p90 чужой | макс.чужой | запас");
        foreach (var group in rows
            .GroupBy(static row => row.ReferenceId, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => Percentile(
                group.Where(static row => !row.IsOwn).Select(static row => row.Score),
                0.9)))
        {
            var own = group.Where(static row => row.IsOwn).Select(static row => row.Score).ToArray();
            var other = group.Where(static row => !row.IsOwn).Select(static row => row.Score).ToArray();
            var otherP90 = Percentile(other, 0.9);
            var ownMinimum = own.Length == 0 ? double.NaN : own.Min();
            Console.WriteLine(
                $"{group.Key,-16} | {own.Length,5} | " +
                $"{(own.Length == 0 ? "  —  " : ownMinimum.ToString("F3")),8} | " +
                $"{other.Length,5} | {other.Average():F3}    | {otherP90:F3}     | " +
                $"{other.Max():F3}      | " +
                $"{(own.Length == 0 ? "  —  " : (ownMinimum - otherP90).ToString("+0.000;-0.000")),7}");
        }

        Console.WriteLine();
        Console.WriteLine("=== НАИБОЛЕЕ ОПАСНЫЕ ЧУЖИЕ СРАБАТЫВАНИЯ ===");
        foreach (var row in rows
            .Where(static row => !row.IsOwn)
            .OrderByDescending(static row => row.Score)
            .Take(15))
        {
            Console.WriteLine(
                $"  {row.ReferenceId,-14} = {row.Score:F3} на области [{row.RegionLabel}] ({row.Frame})");
        }

        return 0;
    }

    public static async Task<int> PrintNegativeMarginsAsync(string corpusDirectory)
    {
        var real = new List<MarginRow>();
        var fake = new List<MarginRow>();
        var runner = new MinimapReplayRunner();
        foreach (var path in ReplaySampleFile.EnumerateCorpus(corpusDirectory))
        {
            if (await ReplaySampleFile.TryLoadLabelsAsync(path) is not { } truth)
            {
                continue;
            }

            var sample = await ReplaySampleFile.LoadAsync(path);
            var frame = ReplaySampleFile.Name(path);
            var champions = truth.Regions
                .Where(static region => region.Kind == MinimapGroundTruthKind.Champion)
                .ToArray();
            foreach (var region in runner.RunRegionDiagnostics(sample))
            {
                var centerX = region.Proposal.X + ((region.Proposal.Width - 1) / 2d);
                var centerY = region.Proposal.Y + ((region.Proposal.Height - 1) / 2d);
                var nearest = champions
                    .Select(champion => new
                    {
                        Champion = champion,
                        Distance = Math.Sqrt(
                            ((champion.CenterX - centerX) * (champion.CenterX - centerX)) +
                            ((champion.CenterY - centerY) * (champion.CenterY - centerY)))
                    })
                    .OrderBy(static candidate => candidate.Distance)
                    .FirstOrDefault();
                var best = region.ReferenceSimilarities.Max(static score => score.Similarity);
                var row = new MarginRow(
                    best - region.NegativeSimilarity,
                    best,
                    region.NegativeSimilarity,
                    $"({region.Proposal.X},{region.Proposal.Y}) {frame}");
                if (nearest is not null && nearest.Distance <= MinimapReplayEvaluator.MatchCenterDistance)
                {
                    real.Add(row with { Note = $"{nearest.Champion.ChampionId} {row.Note}" });
                }
                else
                {
                    fake.Add(row);
                }
            }
        }

        if (real.Count == 0 && fake.Count == 0)
        {
            Console.Error.WriteLine("В каталоге нет размеченных кадров.");
            return 1;
        }

        Console.WriteLine("=== ЗАПАС ЛУЧШЕГО БАЛЛА НАД ОТРИЦАТЕЛЬНЫМИ ЭТАЛОНАМИ ===");
        Describe("настоящие портреты", real);
        Describe("ложные области    ", fake);

        Console.WriteLine();
        Console.WriteLine($"Отсечение при разных требованиях к запасу (ложных всего {fake.Count}):");
        Console.WriteLine("запас | потеряно настоящих | отсечено ложных");
        foreach (var required in new[] { 0.0, 0.02, 0.04, 0.06, 0.08, 0.10, 0.12, 0.15 })
        {
            Console.WriteLine(
                $"{required:F2}  | {real.Count(row => row.Gap < required),18} | " +
                $"{fake.Count(row => row.Gap < required),15}");
        }

        Console.WriteLine();
        Console.WriteLine("Настоящие портреты с наименьшим запасом:");
        foreach (var row in real.OrderBy(static row => row.Gap).Take(6))
        {
            Console.WriteLine(
                $"  запас={row.Gap:F3} балл={row.Best:F3} негативы={row.Negative:F3}  {row.Note}");
        }

        Console.WriteLine("Ложные области с наибольшим запасом:");
        foreach (var row in fake.OrderByDescending(static row => row.Gap).Take(6))
        {
            Console.WriteLine(
                $"  запас={row.Gap:F3} балл={row.Best:F3} негативы={row.Negative:F3}  {row.Note}");
        }

        return 0;
    }

    /// <summary>
    /// Липкость измеряется офлайн: каждый эталон помещается на нейтральный фон, и
    /// замеряются баллы остальных. Статистику чужих баллов нельзя набрать по живым
    /// кадрам, потому что каждый чемпион встречается в считаных матчах.
    /// </summary>
    public static async Task<int> PrintStickinessAsync(string path)
    {
        var sample = await ReplaySampleFile.LoadAsync(path);
        var runner = new MinimapReplayRunner();
        const int side = MinimapChampionReference.ImageSide;
        const int placement = (CanvasSide - side) / 2;

        Console.WriteLine($"=== ЛИПКОСТЬ ЭТАЛОНОВ {ReplaySampleFile.Name(path)} ===");
        Console.WriteLine("эталон           | свой  | чужой ср | чужой макс | ближайший чужой");
        var rows = new List<(string Id, double Own, double Mean, double Max, string Worst)>();
        foreach (var placed in sample.ChampionReferences)
        {
            var canvas = new byte[CanvasSide * CanvasSide * 4];
            for (var index = 0; index < canvas.Length; index += 4)
            {
                canvas[index] = 60;
                canvas[index + 1] = 70;
                canvas[index + 2] = 55;
                canvas[index + 3] = 255;
            }

            var source = placed.BgraPixels.Span;
            for (var row = 0; row < side; row++)
            {
                for (var column = 0; column < side; column++)
                {
                    var from = ((row * side) + column) * 4;
                    var to = (((placement + row) * CanvasSide) + placement + column) * 4;
                    canvas[to] = source[from];
                    canvas[to + 1] = source[from + 1];
                    canvas[to + 2] = source[from + 2];
                    canvas[to + 3] = 255;
                }
            }

            var synthetic = new MinimapReplaySample(
                sample.CapturedAt,
                CanvasSide,
                CanvasSide,
                CanvasSide * 4,
                canvas,
                sample.ChampionReferences,
                sample.Baseline);
            var scores = runner
                .ProbeRegion(synthetic, placement + (side / 2) - 12, placement + (side / 2) - 12)
                .ReferenceSimilarities;
            var own = scores
                .First(score => string.Equals(
                    score.ChampionId,
                    placed.ChampionId,
                    StringComparison.OrdinalIgnoreCase))
                .Similarity;
            var others = scores
                .Where(score => !string.Equals(
                    score.ChampionId,
                    placed.ChampionId,
                    StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(static score => score.Similarity)
                .ToArray();
            rows.Add((
                placed.ChampionId,
                own,
                others.Average(static score => score.Similarity),
                others[0].Similarity,
                others[0].ChampionId));
        }

        foreach (var row in rows.OrderByDescending(static row => row.Max))
        {
            Console.WriteLine(
                $"{row.Id,-16} | {row.Own:F3} | {row.Mean:F3}    | {row.Max:F3}      | {row.Worst}");
        }

        return 0;
    }

    private static void Describe(string title, List<MarginRow> rows)
    {
        if (rows.Count == 0)
        {
            Console.WriteLine($"{title}: нет данных");
            return;
        }

        var gaps = rows.Select(static row => row.Gap).OrderBy(static gap => gap).ToArray();
        Console.WriteLine(
            $"{title}: n={gaps.Length} мин={gaps[0]:F3} " +
            $"p10={gaps[Math.Max(0, (gaps.Length / 10) - 1)]:F3} " +
            $"медиана={gaps[gaps.Length / 2]:F3} " +
            $"p90={gaps[Math.Min(gaps.Length - 1, gaps.Length * 9 / 10)]:F3} " +
            $"макс={gaps[^1]:F3}");
    }

    private static double Percentile(IEnumerable<double> values, double fraction)
    {
        var sorted = values.OrderBy(static value => value).ToArray();
        if (sorted.Length == 0)
        {
            return double.NaN;
        }

        var index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    private sealed record ScoreRow(
        string Frame,
        string ReferenceId,
        double Score,
        bool IsOwn,
        string RegionLabel);

    private sealed record MarginRow(double Gap, double Best, double Negative, string Note);
}
