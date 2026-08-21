namespace TempoCue.Vision;

/// <summary>
/// Хранит наблюдаемый результат распознавания одного кадра без OpenCV-типов.
/// </summary>
public sealed record MinimapReplayOutcome(
    MinimapMarkerCandidate[] MarkerCandidates,
    int ChampionProposalCount,
    MinimapChampionMarkerCandidate[] ChampionCandidates,
    double MaximumIdentityConfidence);
