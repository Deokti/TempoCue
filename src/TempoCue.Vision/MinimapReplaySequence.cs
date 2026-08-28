namespace TempoCue.Vision;

/// <summary>
/// Несколько подряд идущих кадров миникарты одного матча.
///
/// Корпус из независимых кадров отвечает на вопрос «скольких участников
/// распознаватель находит», но молчит о том, находит ли он их **на каждом кадре**.
/// Игроку эти случаи ощущаются по-разному: отметка либо держится на чемпионе, либо
/// прыгает. Последовательность существует ровно для этого измерения.
///
/// Эталоны состава хранятся один раз: внутри матча ростер не меняется.
/// </summary>
public sealed class MinimapReplaySequence
{
    private readonly MinimapReplaySequenceFrame[] _frames;
    private readonly MinimapChampionReference[] _championReferences;

    public MinimapReplaySequence(
        int imageWidth,
        int imageHeight,
        int imageStride,
        IReadOnlyList<MinimapReplaySequenceFrame> frames,
        IReadOnlyList<MinimapChampionReference> championReferences)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(championReferences);
        if (imageStride < checked(imageWidth * 4))
        {
            throw new ArgumentOutOfRangeException(nameof(imageStride));
        }

        if (frames.Count == 0)
        {
            throw new ArgumentException("Последовательность не может быть пустой.", nameof(frames));
        }

        var expectedLength = checked(imageStride * imageHeight);
        foreach (var frame in frames)
        {
            if (frame.BgraPixels.Length != expectedLength)
            {
                throw new ArgumentException(
                    "Размер кадра не соответствует объявленной геометрии.",
                    nameof(frames));
            }
        }

        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        ImageStride = imageStride;
        _frames = [.. frames];
        _championReferences = [.. championReferences];
    }

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    public int ImageStride { get; }

    public IReadOnlyList<MinimapReplaySequenceFrame> Frames => _frames;

    public IReadOnlyList<MinimapChampionReference> ChampionReferences => _championReferences;

    /// <summary>Промежуток между первым и последним кадром записи.</summary>
    public TimeSpan Duration => _frames.Length < 2
        ? TimeSpan.Zero
        : _frames[^1].CapturedAt - _frames[0].CapturedAt;

    /// <summary>
    /// Разбирает последовательность на отдельные кадры, чтобы существующие
    /// инструменты работали с ними без изменений.
    /// </summary>
    public MinimapReplaySample ToSample(int frameIndex) =>
        new(
            _frames[frameIndex].CapturedAt,
            ImageWidth,
            ImageHeight,
            ImageStride,
            _frames[frameIndex].BgraPixels.Span,
            _championReferences,
            new MinimapReplayOutcome([], 0, [], 0));
}

/// <summary>Один кадр последовательности со своим временем захвата.</summary>
public sealed record MinimapReplaySequenceFrame(
    DateTimeOffset CapturedAt,
    ReadOnlyMemory<byte> BgraPixels);

/// <summary>
/// Разметка последовательности. Намеренно грубее покадровой: для измерения
/// устойчивости достаточно знать, кто виден на протяжении всей записи, а
/// размечать каждый значок на каждом кадре — работа, которой никто не станет
/// делать регулярно.
/// </summary>
public sealed record MinimapSequenceGroundTruth(
    int SchemaVersion,
    IReadOnlyList<string> VisibleThroughout,
    string? Notes)
{
    public const int CurrentSchemaVersion = 1;
}
