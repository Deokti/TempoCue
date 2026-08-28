namespace TempoCue.Vision;

/// <summary>
/// Полный разбор канонического кадра миникарты: от поиска кандидатов до их
/// сопоставления с составом матча. В отличие от <see cref="IMinimapChampionRecognizer"/>,
/// который отвечает на вопрос «кто в этой области», контракт покрывает и вопрос
/// «где вообще искать». Это позволяет заменить весь конвейер целиком — например,
/// полнокадровым детектором, который не нуждается в готовых областях.
/// </summary>
public interface IMinimapFrameAnalyzer
{
    MinimapFrameAnalysis Analyze(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionReference> championReferences);
}

/// <summary>Результат разбора одного кадра, независимый от способа распознавания.</summary>
public sealed record MinimapFrameAnalysis(
    IReadOnlyList<MinimapMarkerCandidate> MarkerCandidates,
    int ProposalCount,
    IReadOnlyList<MinimapChampionMarkerCandidate> ChampionCandidates,
    double MaximumIdentityConfidence)
{
    public static MinimapFrameAnalysis Empty { get; } = new([], 0, [], 0);
}

/// <summary>Штатный конвейер: цветные контуры, области сравнения и эталоны портретов.</summary>
public sealed class DefaultMinimapFrameAnalyzer : IMinimapFrameAnalyzer, IDisposable
{
    private readonly IMinimapChampionRecognizer _championRecognizer;
    private readonly MinimapRecognitionPipeline _pipeline;

    public DefaultMinimapFrameAnalyzer()
        : this(new OpenCvMinimapChampionRecognizer())
    {
    }

    /// <param name="maximumDegreeOfParallelism">
    /// Сколько областей кадра сравнивается одновременно. На результат не влияет,
    /// только на время разбора одного кадра.
    /// </param>
    public DefaultMinimapFrameAnalyzer(int maximumDegreeOfParallelism)
        : this(new OpenCvMinimapChampionRecognizer(maximumDegreeOfParallelism))
    {
    }

    /// <summary>
    /// Половина логических процессоров: распознавание делит машину с игрой.
    /// </summary>
    public static int DefaultDegreeOfParallelism =>
        OpenCvMinimapChampionRecognizer.DefaultDegreeOfParallelism;

    internal DefaultMinimapFrameAnalyzer(IMinimapChampionRecognizer championRecognizer)
    {
        _championRecognizer = championRecognizer;
        _pipeline = new MinimapRecognitionPipeline(championRecognizer);
    }

    public MinimapFrameAnalysis Analyze(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionReference> championReferences)
    {
        var result = _pipeline.Analyze(pixels, width, height, stride, championReferences);
        return new MinimapFrameAnalysis(
            result.MarkerCandidates,
            result.ChampionProposalCount,
            result.ChampionCandidates,
            result.MaximumIdentityConfidence);
    }

    public void Dispose() => (_championRecognizer as IDisposable)?.Dispose();
}
