namespace TempoCue.Vision;

/// <summary>Результат одного прохода распознавания портретов на миникарте.</summary>
internal sealed record MinimapChampionRecognitionResult(
    MinimapChampionMarkerCandidate[] Candidates,
    double MaximumObservedSimilarity);
