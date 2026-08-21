namespace TempoCue.Vision;

/// <summary>Повторно запускает живой конвейер распознавания на сохранённом кадре.</summary>
public sealed class MinimapReplayRunner
{
    private readonly MinimapRecognitionPipeline _pipeline;

    public MinimapReplayRunner()
        : this(new OpenCvMinimapChampionRecognizer())
    {
    }

    internal MinimapReplayRunner(IMinimapChampionRecognizer championRecognizer)
    {
        _pipeline = new MinimapRecognitionPipeline(championRecognizer);
    }

    public MinimapReplayRunResult Run(MinimapReplaySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var pipelineResult = _pipeline.Analyze(
            sample.BgraPixels,
            sample.ImageWidth,
            sample.ImageHeight,
            sample.ImageStride,
            sample.ChampionReferences);
        var actual = new MinimapReplayOutcome(
            pipelineResult.MarkerCandidates,
            pipelineResult.ChampionProposalCount,
            pipelineResult.ChampionCandidates,
            pipelineResult.MaximumIdentityConfidence);
        var baselineAssignments = sample.Baseline.ChampionCandidates
            .Select(RecognitionAssignment.FromCandidate)
            .ToHashSet();
        var actualAssignments = actual.ChampionCandidates
            .Select(RecognitionAssignment.FromCandidate)
            .ToHashSet();

        return new MinimapReplayRunResult(
            actual,
            baselineAssignments.Intersect(actualAssignments).Count(),
            baselineAssignments.Except(actualAssignments).Count(),
            actualAssignments.Except(baselineAssignments).Count());
    }

    private sealed record RecognitionAssignment(
        string? ChampionId,
        MinimapMarkerAffiliation Affiliation,
        int X,
        int Y,
        int Width,
        int Height)
    {
        public static RecognitionAssignment FromCandidate(
            MinimapChampionMarkerCandidate candidate) =>
            new(
                candidate.ChampionId,
                candidate.Affiliation,
                candidate.X,
                candidate.Y,
                candidate.Width,
                candidate.Height);
    }
}

/// <summary>
/// Показывает, какие назначения сохранились, исчезли или появились относительно baseline.
/// Baseline фиксирует прежнее поведение, но не считается ручной разметкой истины.
/// </summary>
public sealed record MinimapReplayRunResult(
    MinimapReplayOutcome Actual,
    int MatchedBaselineAssignments,
    int MissingBaselineAssignments,
    int UnexpectedAssignments)
{
    public bool MatchesBaseline =>
        MissingBaselineAssignments == 0 && UnexpectedAssignments == 0;
}
