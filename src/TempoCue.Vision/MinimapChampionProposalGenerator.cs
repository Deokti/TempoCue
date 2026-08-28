namespace TempoCue.Vision;

/// <summary>
/// Преобразует цветной контур команды в область поиска портрета чемпиона.
/// Никакие геометрические признаки здесь не подтверждают чемпиона: окончательное
/// решение принимает сопоставитель с портретами участников активного матча.
/// </summary>
internal static class MinimapChampionProposalGenerator
{
    private const int MarkerSide = 25;
    private const int MarkerRadius = MarkerSide / 2;
    private const int DuplicateCenterDistance = 8;

    public static MinimapChampionMarkerCandidate[] Create(
        int imageWidth,
        int imageHeight,
        IReadOnlyList<MinimapMarkerCandidate> candidates)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
        ArgumentNullException.ThrowIfNull(candidates);

        var proposals = candidates
            .Select(candidate => CreateProposal(candidate, imageWidth, imageHeight))
            .Where(candidate => candidate is not null)
            .Cast<MinimapChampionMarkerCandidate>()
            .OrderByDescending(candidate => candidate.Confidence)
            .ToArray();

        var unique = new List<MinimapChampionMarkerCandidate>(proposals.Length);
        var maximumDistanceSquared = DuplicateCenterDistance * DuplicateCenterDistance;
        foreach (var proposal in proposals)
        {
            var centerX = proposal.X + MarkerRadius;
            var centerY = proposal.Y + MarkerRadius;
            if (unique.Any(existing =>
            {
                var deltaX = centerX - (existing.X + MarkerRadius);
                var deltaY = centerY - (existing.Y + MarkerRadius);
                return (deltaX * deltaX) + (deltaY * deltaY) <= maximumDistanceSquared;
            }))
            {
                continue;
            }

            unique.Add(proposal);
        }

        return unique
            .OrderBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X)
            .ToArray();
    }

    private static MinimapChampionMarkerCandidate? CreateProposal(
        MinimapMarkerCandidate candidate,
        int imageWidth,
        int imageHeight)
    {
        var centerX = candidate.X + ((candidate.Width - 1) / 2d);
        var centerY = candidate.Y + ((candidate.Height - 1) / 2d);
        var x = (int)Math.Round(centerX) - MarkerRadius;
        var y = (int)Math.Round(centerY) - MarkerRadius;
        if (x < 0 || y < 0 || x > imageWidth - MarkerSide || y > imageHeight - MarkerSide)
        {
            return null;
        }

        return new MinimapChampionMarkerCandidate(
            x,
            y,
            MarkerSide,
            MarkerSide,
            candidate.Affiliation,
            candidate.Confidence);
    }
}
