namespace TempoCue.Vision;

/// <summary>
/// Выполняет один и тот же проход распознавания для живого кадра и офлайн-воспроизведения.
/// </summary>
internal sealed class MinimapRecognitionPipeline
{
    private readonly IMinimapChampionRecognizer _championRecognizer;

    public MinimapRecognitionPipeline(IMinimapChampionRecognizer championRecognizer)
    {
        _championRecognizer = championRecognizer ??
            throw new ArgumentNullException(nameof(championRecognizer));
    }

    public MinimapRecognitionPipelineResult Analyze(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionReference> championReferences)
    {
        ArgumentNullException.ThrowIfNull(championReferences);

        var markerCandidates = MinimapMarkerCandidateDetector.Detect(
            pixels.Span,
            width,
            height,
            stride);
        var championProposals = MinimapChampionProposalGenerator.Create(
            width,
            height,
            markerCandidates);
        var recognition = _championRecognizer.Recognize(
            pixels,
            width,
            height,
            stride,
            championProposals,
            championReferences);

        return new MinimapRecognitionPipelineResult(
            markerCandidates,
            championProposals.Length,
            recognition.Candidates,
            recognition.MaximumObservedSimilarity);
    }
}

/// <summary>Результат полного диагностического прохода по одному кадру миникарты.</summary>
internal sealed record MinimapRecognitionPipelineResult(
    MinimapMarkerCandidate[] MarkerCandidates,
    int ChampionProposalCount,
    MinimapChampionMarkerCandidate[] ChampionCandidates,
    double MaximumIdentityConfidence);
