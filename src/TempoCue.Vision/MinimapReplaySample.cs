namespace TempoCue.Vision;

/// <summary>
/// Самодостаточный обезличенный вход для повторного запуска распознавания миникарты.
/// </summary>
public sealed class MinimapReplaySample
{
    private const int BytesPerPixel = 4;
    private readonly byte[] _bgraPixels;
    private readonly MinimapChampionReference[] _championReferences;

    public MinimapReplaySample(
        DateTimeOffset capturedAt,
        int imageWidth,
        int imageHeight,
        int imageStride,
        ReadOnlySpan<byte> bgraPixels,
        IReadOnlyList<MinimapChampionReference> championReferences,
        MinimapReplayOutcome baseline)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
        if (imageStride < checked(imageWidth * BytesPerPixel))
        {
            throw new ArgumentOutOfRangeException(nameof(imageStride));
        }

        var requiredLength = checked(imageStride * imageHeight);
        if (bgraPixels.Length != requiredLength)
        {
            throw new ArgumentException(
                "Размер массива должен точно соответствовать изображению BGRA32.",
                nameof(bgraPixels));
        }

        ArgumentNullException.ThrowIfNull(championReferences);
        ArgumentNullException.ThrowIfNull(baseline);
        if (championReferences.Count > 10)
        {
            throw new ArgumentOutOfRangeException(
                nameof(championReferences),
                "Диагностический кадр не может содержать больше десяти участников.");
        }

        CapturedAt = capturedAt;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        ImageStride = imageStride;
        _bgraPixels = bgraPixels.ToArray();
        _championReferences = championReferences.ToArray();
        Baseline = baseline with
        {
            MarkerCandidates = baseline.MarkerCandidates.ToArray(),
            ChampionCandidates = baseline.ChampionCandidates.ToArray()
        };
    }

    public DateTimeOffset CapturedAt { get; }

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    public int ImageStride { get; }

    public ReadOnlyMemory<byte> BgraPixels => _bgraPixels;

    public IReadOnlyList<MinimapChampionReference> ChampionReferences =>
        _championReferences;

    public MinimapReplayOutcome Baseline { get; }

    public static MinimapReplaySample FromPreview(
        MinimapPreviewSnapshot preview,
        IReadOnlyList<MinimapChampionReference> championReferences)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return new MinimapReplaySample(
            preview.CapturedAt,
            preview.ImageWidth,
            preview.ImageHeight,
            preview.ImageStride,
            preview.BgraPixels,
            championReferences,
            new MinimapReplayOutcome(
                preview.MarkerCandidates.ToArray(),
                preview.ChampionProposalCount,
                preview.ChampionMarkerCandidates.ToArray(),
                preview.MaximumIdentityConfidence));
    }
}
