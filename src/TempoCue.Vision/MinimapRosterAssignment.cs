namespace TempoCue.Vision;

/// <summary>
/// Детерминированно назначает областям участников активного состава после того,
/// как конкретный распознаватель уже вычислил сходство изображений.
/// </summary>
internal static class MinimapRosterAssignment
{
    private const int DuplicateCenterDistance = 10;
    private const double MinimumIdentityConfidence = 0.75;
    private const double MinimumIdentityMargin = 0.04;

    public static MinimapChampionMarkerCandidate[] Select(
        IReadOnlyList<MinimapRosterCandidateScore> candidateScores)
    {
        ArgumentNullException.ThrowIfNull(candidateScores);

        var selected = new List<SelectedMatch>(candidateScores.Count);
        var assignedCandidates = new HashSet<int>();
        var assignedParticipants = new HashSet<(
            string ChampionId,
            MinimapMarkerAffiliation Affiliation)>();

        while (true)
        {
            SelectedMatch? proposal = null;
            foreach (var scores in candidateScores)
            {
                if (assignedCandidates.Contains(scores.CandidateIndex) ||
                    selected.Any(existing => AreDuplicateRegions(
                        existing.Candidate,
                        scores.Candidate)))
                {
                    continue;
                }

                var current = CreateUnambiguousProposal(
                    scores,
                    assignedParticipants);
                if (current is not null &&
                    (proposal is null || HasHigherPriority(current, proposal)))
                {
                    proposal = current;
                }
            }

            if (proposal is null)
            {
                break;
            }

            assignedCandidates.Add(proposal.CandidateIndex);
            assignedParticipants.Add((
                proposal.Reference.ChampionId,
                proposal.Reference.Affiliation));
            selected.Add(proposal);
        }

        return selected
            .OrderBy(static match => match.CandidateIndex)
            .Select(static match => match.Candidate with
            {
                Affiliation = match.Reference.Affiliation,
                ChampionId = match.Reference.ChampionId,
                IdentityConfidence = match.Similarity,
                Confidence = (match.Candidate.Confidence + match.Similarity) / 2,
                ContourAffiliation = match.Candidate.Affiliation
            })
            .ToArray();
    }

    private static SelectedMatch? CreateUnambiguousProposal(
        MinimapRosterCandidateScore scores,
        IReadOnlySet<(string ChampionId, MinimapMarkerAffiliation Affiliation)> assignedParticipants)
    {
        var originalBest = scores.ReferenceScores[0];
        var originalBestParticipant = (
            originalBest.Reference.ChampionId,
            originalBest.Reference.Affiliation);
        if (!assignedParticipants.Contains(originalBestParticipant))
        {
            var originalSecond = scores.ReferenceScores.Count > 1
                ? scores.ReferenceScores[1].Similarity
                : -1;
            return originalBest.Similarity >= MinimumIdentityConfidence &&
                originalBest.Similarity > scores.NegativeSimilarity &&
                originalBest.Similarity - originalSecond >= MinimumIdentityMargin
                    ? new SelectedMatch(
                        scores.CandidateIndex,
                        scores.Candidate,
                        originalBest.Reference,
                        originalBest.Similarity)
                    : null;
        }

        // Если соседние портреты визуально похожи, обе области могут сначала
        // выбрать одного участника. После его более сильного назначения разрешаем
        // только близкую по баллу альтернативу той же стороны контура. Далёкие
        // «оставшиеся» участники не должны распределяться по башням и миньонам.
        MinimapRosterReferenceScore? bestAvailable = null;
        MinimapRosterReferenceScore? secondAvailable = null;
        foreach (var score in scores.ReferenceScores)
        {
            if (score.Similarity < MinimumIdentityConfidence ||
                score.Similarity <= scores.NegativeSimilarity ||
                score.Reference.Affiliation != scores.Candidate.Affiliation ||
                assignedParticipants.Contains((
                    score.Reference.ChampionId,
                    score.Reference.Affiliation)))
            {
                continue;
            }

            if (bestAvailable is null)
            {
                bestAvailable = score;
            }
            else
            {
                secondAvailable = score;
                break;
            }
        }

        if (bestAvailable is null ||
            originalBest.Similarity - bestAvailable.Similarity >= MinimumIdentityMargin ||
            secondAvailable is not null &&
            bestAvailable.Similarity - secondAvailable.Similarity < MinimumIdentityMargin)
        {
            return null;
        }

        return new SelectedMatch(
            scores.CandidateIndex,
            scores.Candidate,
            bestAvailable.Reference,
            bestAvailable.Similarity);
    }

    private static bool HasHigherPriority(
        SelectedMatch candidate,
        SelectedMatch current)
    {
        var similarityComparison = candidate.Similarity.CompareTo(current.Similarity);
        if (similarityComparison != 0)
        {
            return similarityComparison > 0;
        }

        var yComparison = candidate.Candidate.Y.CompareTo(current.Candidate.Y);
        if (yComparison != 0)
        {
            return yComparison < 0;
        }

        var xComparison = candidate.Candidate.X.CompareTo(current.Candidate.X);
        return xComparison != 0
            ? xComparison < 0
            : string.CompareOrdinal(
                candidate.Reference.ChampionId,
                current.Reference.ChampionId) < 0;
    }

    private static bool AreDuplicateRegions(
        MinimapChampionMarkerCandidate first,
        MinimapChampionMarkerCandidate second)
    {
        var firstCenterX = first.X + (first.Width / 2d);
        var firstCenterY = first.Y + (first.Height / 2d);
        var secondCenterX = second.X + (second.Width / 2d);
        var secondCenterY = second.Y + (second.Height / 2d);
        var deltaX = firstCenterX - secondCenterX;
        var deltaY = firstCenterY - secondCenterY;
        return (deltaX * deltaX) + (deltaY * deltaY) <=
            DuplicateCenterDistance * DuplicateCenterDistance;
    }

    private sealed record SelectedMatch(
        int CandidateIndex,
        MinimapChampionMarkerCandidate Candidate,
        MinimapChampionReference Reference,
        double Similarity);
}

/// <param name="ScaleIndex">
/// Масштаб шаблона, на котором взят лучший балл. Нужен для замера: если побеждает
/// почти всегда один и тот же, перебор пяти масштабов можно сузить и потратить
/// освободившееся на более подробное сравнение.
/// </param>
internal sealed record MinimapRosterReferenceScore(
    MinimapChampionReference Reference,
    double Similarity,
    int ScaleIndex);

internal sealed record MinimapRosterCandidateScore(
    int CandidateIndex,
    MinimapChampionMarkerCandidate Candidate,
    IReadOnlyList<MinimapRosterReferenceScore> ReferenceScores,
    double NegativeSimilarity);
