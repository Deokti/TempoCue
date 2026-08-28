namespace TempoCue.Vision;

/// <summary>
/// Сравнивает подтверждённые распознаванием портреты с ручной разметкой кадра
/// и раскладывает результат на совпадения, пропуски и ложные срабатывания.
/// </summary>
public static class MinimapReplayEvaluator
{
    /// <summary>
    /// Максимальное расстояние между центрами области распознавания и разметки.
    /// Совпадает с половиной стороны области поиска портрета, чтобы ручная
    /// неточность прямоугольника не превращала попадание в промах.
    /// </summary>
    public const double MatchCenterDistance = 13;

    public static MinimapReplayEvaluation Evaluate(
        MinimapReplayOutcome outcome,
        MinimapReplayGroundTruth groundTruth)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentNullException.ThrowIfNull(groundTruth);

        var championRegions = groundTruth.Regions
            .Where(static region => region.Kind == MinimapGroundTruthKind.Champion)
            .ToArray();
        var consumed = new bool[championRegions.Length];
        var confirmed = new List<MinimapReplayConfirmedChampion>();
        var falsePositives = new List<MinimapReplayFalsePositive>();

        foreach (var candidate in outcome.ChampionCandidates)
        {
            var matchIndex = FindMatchingChampionRegion(candidate, championRegions, consumed);
            if (matchIndex >= 0)
            {
                consumed[matchIndex] = true;
                confirmed.Add(new MinimapReplayConfirmedChampion(
                    candidate,
                    championRegions[matchIndex]));
                continue;
            }

            falsePositives.Add(new MinimapReplayFalsePositive(
                candidate,
                FindNearestRegion(candidate, groundTruth.Regions)));
        }

        var missedChampions = championRegions
            .Where((_, index) => !consumed[index])
            .ToArray();
        return new MinimapReplayEvaluation(confirmed, missedChampions, falsePositives);
    }

    private static int FindMatchingChampionRegion(
        MinimapChampionMarkerCandidate candidate,
        IReadOnlyList<MinimapGroundTruthRegion> championRegions,
        bool[] consumed)
    {
        var bestIndex = -1;
        var bestDistance = double.MaxValue;
        for (var index = 0; index < championRegions.Count; index++)
        {
            if (consumed[index])
            {
                continue;
            }

            var region = championRegions[index];
            if (!string.Equals(
                    region.ChampionId,
                    candidate.ChampionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (region.Affiliation is { } affiliation &&
                affiliation != candidate.Affiliation)
            {
                continue;
            }

            var distance = CenterDistance(candidate, region);
            if (distance <= MatchCenterDistance && distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static MinimapGroundTruthRegion? FindNearestRegion(
        MinimapChampionMarkerCandidate candidate,
        IReadOnlyList<MinimapGroundTruthRegion> regions)
    {
        MinimapGroundTruthRegion? nearest = null;
        var bestDistance = double.MaxValue;
        foreach (var region in regions)
        {
            var distance = CenterDistance(candidate, region);
            if (distance <= MatchCenterDistance && distance < bestDistance)
            {
                bestDistance = distance;
                nearest = region;
            }
        }

        return nearest;
    }

    private static double CenterDistance(
        MinimapChampionMarkerCandidate candidate,
        MinimapGroundTruthRegion region)
    {
        var deltaX = candidate.X + (candidate.Width / 2d) - region.CenterX;
        var deltaY = candidate.Y + (candidate.Height / 2d) - region.CenterY;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
    }
}

/// <summary>Подтверждение, совпавшее с размеченным чемпионом по личности и месту.</summary>
public sealed record MinimapReplayConfirmedChampion(
    MinimapChampionMarkerCandidate Candidate,
    MinimapGroundTruthRegion Region);

/// <summary>
/// Ложное подтверждение. Ближайшая размеченная область называет причину —
/// башню, миньонов, тотем или чужого чемпиона; null означает объект вне разметки.
/// </summary>
public sealed record MinimapReplayFalsePositive(
    MinimapChampionMarkerCandidate Candidate,
    MinimapGroundTruthRegion? NearestRegion);

/// <summary>Результат сравнения одного кадра с ручной разметкой.</summary>
public sealed record MinimapReplayEvaluation(
    IReadOnlyList<MinimapReplayConfirmedChampion> ConfirmedChampions,
    IReadOnlyList<MinimapGroundTruthRegion> MissedChampions,
    IReadOnlyList<MinimapReplayFalsePositive> FalsePositives)
{
    public double Precision =>
        ConfirmedChampions.Count + FalsePositives.Count == 0
            ? 1
            : ConfirmedChampions.Count /
              (double)(ConfirmedChampions.Count + FalsePositives.Count);

    public double Recall =>
        ConfirmedChampions.Count + MissedChampions.Count == 0
            ? 1
            : ConfirmedChampions.Count /
              (double)(ConfirmedChampions.Count + MissedChampions.Count);

    /// <summary>
    /// Проверяет кадр против допусков его разметки. Допуски фиксируют известное
    /// поведение, поэтому нарушение означает ухудшение распознавания.
    /// </summary>
    public bool SatisfiesAllowances(MinimapReplayGroundTruth groundTruth)
    {
        ArgumentNullException.ThrowIfNull(groundTruth);
        return FalsePositives.Count <= groundTruth.AllowedFalsePositives &&
            MissedChampions.Count <= groundTruth.AllowedMissedChampions;
    }
}
