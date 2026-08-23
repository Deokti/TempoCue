using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace TempoCue.Vision.DeeperLeague;

/// <summary>
/// Экспериментальный распознаватель на сторонней модели DeeperLeague. Принимает
/// тот же канонический кадр миникарты, что и основной конвейер, и возвращает те же
/// кандидаты, поэтому оба пути сравнимы одним корпусным измерением.
/// </summary>
public sealed class DeeperLeagueMinimapRecognizer : IDisposable
{
    /// <summary>Размер входа сети; модель обучалась с imgsz = 640.</summary>
    public const int NetworkSide = 640;

    private const int BytesPerPixel = 4;
    private const double DefaultMinimumConfidence = 0.25;
    private const double DefaultMaximumOverlap = 0.45;

    /// <summary>
    /// Порог сходства со статичным объектом, начиная с которого предсказание
    /// отклоняется. Значение подобрано по корпусу и требует перепроверки при
    /// пополнении набора отрицательных эталонов.
    /// </summary>
    private const double StaticObjectRejectionSimilarity = 0.86;

    /// <summary>
    /// Столько же, сколько берёт штатный конвейер: четверть логических
    /// процессоров, но не меньше двух.
    /// </summary>
    private static int DefaultInferenceThreads =>
        DefaultMinimapFrameAnalyzer.DefaultDegreeOfParallelism;

    private readonly InferenceSession _session;
    private readonly DeeperLeagueChampionMap _championMap;
    private readonly StaticObjectFilter? _staticObjectFilter;
    private readonly string _inputName;
    private readonly double _minimumConfidence;
    private readonly double _maximumOverlap;
    private readonly byte[] _resizedFrame;
    private readonly DenseTensor<float> _input;
    private bool _isDisposed;

    /// <param name="maximumDegreeOfParallelism">
    /// Сколько потоков разрешено инференсу. По умолчанию ONNX Runtime создаёт
    /// собственный пул по числу ядер и о настройке приложения не знает, поэтому
    /// без явного ограничения обещание оставить процессор игре не выполняется.
    /// </param>
    public DeeperLeagueMinimapRecognizer(
        string modelPath,
        string championMapPath,
        double minimumConfidence = DefaultMinimumConfidence,
        double maximumOverlap = DefaultMaximumOverlap,
        bool rejectStaticObjects = true,
        int? maximumDegreeOfParallelism = null)
    {
        _staticObjectFilter = rejectStaticObjects ? new StaticObjectFilter() : null;
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException($"Файл модели не найден: {modelPath}");
        }

        _championMap = DeeperLeagueChampionMap.Load(championMapPath);
        // Одна сессия на весь прогон: создание сессии стоит дороже самого инференса.
        using var sessionOptions = new SessionOptions
        {
            IntraOpNumThreads = maximumDegreeOfParallelism ?? DefaultInferenceThreads,
            // Кадр обрабатывается по одному, параллелить нечего между собой.
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };
        // Между кадрами рабочие потоки по умолчанию крутятся в ожидании вместо
        // того, чтобы уснуть: так они просыпаются быстрее, но всё это время жгут
        // процессор. Нам обмен невыгоден — кадры приходят раз в 125 мс, а рядом
        // работает игра, для которой несколько миллисекунд задержки распознавания
        // дешевле отнятого ядра.
        sessionOptions.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        _session = new InferenceSession(modelPath, sessionOptions);
        _inputName = _session.InputMetadata.Keys.First();
        _minimumConfidence = minimumConfidence;
        _maximumOverlap = maximumOverlap;
        _resizedFrame = new byte[NetworkSide * NetworkSide * BytesPerPixel];
        _input = new DenseTensor<float>([1, 3, NetworkSide, NetworkSide]);
    }

    public string ModelPath { get; } = string.Empty;

    /// <summary>
    /// Распознаёт канонический кадр миникарты. Состав матча ограничивает результат:
    /// модель знает 164 чемпиона, но в матче их десять, и посторонние имена
    /// отбрасываются так же, как это делает основной конвейер.
    /// </summary>
    public MinimapChampionMarkerCandidate[] Recognize(
        ReadOnlySpan<byte> canonicalFrame,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionReference> roster)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        FillInput(canonicalFrame, width, height, stride);
        using var results = _session.Run(
            [NamedOnnxValue.CreateFromTensor(_inputName, _input)]);
        var output = results[0].AsTensor<float>();
        var dimensions = output.Dimensions;
        if (dimensions.Length != 3)
        {
            throw new InvalidDataException(
                $"Ожидался трёхмерный выход модели, получено измерений: {dimensions.Length}.");
        }

        var detections = YoloDetectionDecoder.Decode(
            output.ToArray(),
            dimensions[1],
            dimensions[2],
            _minimumConfidence,
            _maximumOverlap);

        var rosterSides = BuildRosterSides(roster);
        var portraits = roster.ToDictionary(
            static reference => reference.ChampionId,
            static reference => reference,
            StringComparer.OrdinalIgnoreCase);
        var scaleX = width / (double)NetworkSide;
        var scaleY = height / (double)NetworkSide;
        var candidates = new List<MinimapChampionMarkerCandidate>(detections.Count);
        foreach (var detection in detections)
        {
            var championId = _championMap[detection.ClassIndex];
            if (championId is null ||
                !rosterSides.TryGetValue(championId, out var affiliation))
            {
                continue;
            }

            var boxWidth = Math.Max(1, (int)Math.Round(detection.Width * scaleX));
            var boxHeight = Math.Max(1, (int)Math.Round(detection.Height * scaleY));
            var x = (int)Math.Round((detection.CenterX * scaleX) - (boxWidth / 2d));
            var y = (int)Math.Round((detection.CenterY * scaleY) - (boxHeight / 2d));

            // Модель обучалась на синтетике и не видела части элементов интерфейса:
            // круглые значки базы она уверенно принимает за чемпионов. Наши
            // отрицательные эталоны отсекают их, не трогая настоящие портреты.
            if (_staticObjectFilter is not null &&
                portraits.TryGetValue(championId, out var portrait) &&
                _staticObjectFilter.ResemblesStaticObjectMoreThanPortrait(
                    canonicalFrame,
                    width,
                    height,
                    stride,
                    (int)Math.Round(detection.CenterX * scaleX),
                    (int)Math.Round(detection.CenterY * scaleY),
                    portrait))
            {
                continue;
            }

            candidates.Add(new MinimapChampionMarkerCandidate(
                Math.Clamp(x, 0, Math.Max(0, width - boxWidth)),
                Math.Clamp(y, 0, Math.Max(0, height - boxHeight)),
                boxWidth,
                boxHeight,
                affiliation,
                Math.Clamp(detection.Confidence, 0, 1),
                championId,
                Math.Clamp(detection.Confidence, 0, 1),
                affiliation));
        }

        return candidates
            .OrderBy(static candidate => candidate.Y)
            .ThenBy(static candidate => candidate.X)
            .ToArray();
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _session.Dispose();
        _staticObjectFilter?.Dispose();
    }

    /// <summary>
    /// Модель не знает сторон, поэтому принадлежность берётся из состава матча —
    /// так же, как в основном конвейере после подтверждения личности.
    /// </summary>
    private static Dictionary<string, MinimapMarkerAffiliation> BuildRosterSides(
        IReadOnlyList<MinimapChampionReference> roster)
    {
        var sides = new Dictionary<string, MinimapMarkerAffiliation>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var reference in roster)
        {
            sides[reference.ChampionId] = reference.Affiliation;
        }

        return sides;
    }

    private void FillInput(
        ReadOnlySpan<byte> canonicalFrame,
        int width,
        int height,
        int stride)
    {
        // Канонический кадр квадратный, вход сети тоже, поэтому дополнение полями
        // не требуется: достаточно масштабирования, а координаты возвращаются
        // обратным коэффициентом.
        MinimapFrameNormalizer.Normalize(
            canonicalFrame,
            width,
            height,
            stride,
            _resizedFrame,
            NetworkSide,
            NetworkSide,
            NetworkSide * BytesPerPixel);

        const int plane = NetworkSide * NetworkSide;
        var destination = _input.Buffer.Span;
        for (var index = 0; index < plane; index++)
        {
            var offset = index * BytesPerPixel;
            destination[index] = _resizedFrame[offset + 2] / 255f;
            destination[plane + index] = _resizedFrame[offset + 1] / 255f;
            destination[(2 * plane) + index] = _resizedFrame[offset] / 255f;
        }
    }
}
