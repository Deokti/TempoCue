namespace TempoCue.Vision;

/// <summary>Повторно запускает живой конвейер распознавания на сохранённом кадре.</summary>
public sealed class MinimapReplayRunner
{
    private readonly MinimapRecognitionPipeline _pipeline;
    private readonly IMinimapChampionRecognizer _championRecognizer;

    public MinimapReplayRunner()
        : this(new OpenCvMinimapChampionRecognizer())
    {
    }

    /// <param name="maximumDegreeOfParallelism">
    /// Сколько областей кадра сравнивается одновременно. На результат не влияет,
    /// только на время разбора одного кадра.
    /// </param>
    public MinimapReplayRunner(int maximumDegreeOfParallelism)
        : this(new OpenCvMinimapChampionRecognizer(maximumDegreeOfParallelism))
    {
    }

    internal MinimapReplayRunner(IMinimapChampionRecognizer championRecognizer)
    {
        _championRecognizer = championRecognizer;
        _pipeline = new MinimapRecognitionPipeline(championRecognizer);
    }

    /// <summary>
    /// Диагностический срез: сырые баллы каждой области сравнения до назначения
    /// состава и измерение кольца значка. Служит калибровке правил по корпусу и
    /// не участвует в живом распознавании.
    /// </summary>
    public IReadOnlyList<MinimapRegionDiagnostics> RunRegionDiagnostics(
        MinimapReplaySample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (_championRecognizer is not OpenCvMinimapChampionRecognizer recognizer)
        {
            throw new InvalidOperationException(
                "Диагностика областей доступна только для распознавателя OpenCV.");
        }

        var markers = MinimapMarkerCandidateDetector.Detect(
            sample.BgraPixels.Span,
            sample.ImageWidth,
            sample.ImageHeight,
            sample.ImageStride);
        var proposals = MinimapChampionProposalGenerator.Create(
            sample.ImageWidth,
            sample.ImageHeight,
            markers);
        var scores = recognizer.ScoreCandidates(
            sample.BgraPixels,
            sample.ImageWidth,
            sample.ImageHeight,
            sample.ImageStride,
            proposals,
            sample.ChampionReferences);
        var diagnostics = new MinimapRegionDiagnostics[scores.Length];
        for (var index = 0; index < scores.Length; index++)
        {
            var score = scores[index];
            diagnostics[index] = new MinimapRegionDiagnostics(
                score.Candidate,
                score.ReferenceScores
                    .Select(static reference => new MinimapReferenceSimilarity(
                        reference.Reference.ChampionId,
                        reference.Reference.Affiliation,
                        reference.Similarity))
                    .ToArray(),
                score.NegativeSimilarity,
                MinimapIconRingSampler.Sample(
                    sample.BgraPixels.Span,
                    sample.ImageWidth,
                    sample.ImageHeight,
                    sample.ImageStride,
                    score.Candidate));
        }

        return diagnostics;
    }

    /// <summary>
    /// Диагностический зонд одной точки: оценивает область портрета в заданном
    /// месте, даже если конвейер не создал там предложение. Служит измерению
    /// пропусков, невидимых обычной диагностике.
    /// </summary>
    public MinimapRegionDiagnostics ProbeRegion(MinimapReplaySample sample, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(sample);
        if (_championRecognizer is not OpenCvMinimapChampionRecognizer recognizer)
        {
            throw new InvalidOperationException(
                "Диагностика областей доступна только для распознавателя OpenCV.");
        }

        var probe = new MinimapChampionMarkerCandidate(
            x,
            y,
            Width: 25,
            Height: 25,
            MinimapMarkerAffiliation.Ally,
            Confidence: 0);
        var score = recognizer.ScoreCandidates(
            sample.BgraPixels,
            sample.ImageWidth,
            sample.ImageHeight,
            sample.ImageStride,
            [probe],
            sample.ChampionReferences).Single();
        return new MinimapRegionDiagnostics(
            score.Candidate,
            score.ReferenceScores
                .Select(static reference => new MinimapReferenceSimilarity(
                    reference.Reference.ChampionId,
                    reference.Reference.Affiliation,
                    reference.Similarity))
                .ToArray(),
            score.NegativeSimilarity,
            MinimapIconRingSampler.Sample(
                sample.BgraPixels.Span,
                sample.ImageWidth,
                sample.ImageHeight,
                sample.ImageStride,
                score.Candidate));
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

/// <summary>Сырые баллы одной области сравнения до назначения состава.</summary>
public sealed record MinimapRegionDiagnostics(
    MinimapChampionMarkerCandidate Proposal,
    IReadOnlyList<MinimapReferenceSimilarity> ReferenceSimilarities,
    double NegativeSimilarity,
    MinimapRingSampleStats RingStats);

/// <summary>Сходство области с эталоном одного участника.</summary>
public sealed record MinimapReferenceSimilarity(
    string ChampionId,
    MinimapMarkerAffiliation Affiliation,
    double Similarity);
