using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace TempoCue.Vision.OwnModel;

/// <summary>
/// Своя модель: детектор «где значки» плюс опознание «кто это». Принимает тот же
/// канонический кадр, что и штатный конвейер, и возвращает те же кандидаты,
/// поэтому оба пути сравнимы одним корпусным измерением.
/// </summary>
/// <remarks>
/// Порт с образца <c>tools/run_onnx.py</c> модельного репозитория. Образец не
/// иллюстрация, а спецификация: он считает те же кадры теми же весами, и его
/// числа — цель, в которую этот код обязан попасть. Расхождение означает ошибку
/// порта, и искать её надо здесь, а не в модели.
///
/// Подготовка пикселей — выбор каналов, деление на 255, растяжение выреза —
/// живёт внутри самих графов ONNX. Поэтому обе сети принимают сырой BGRA ровно
/// в том виде, в каком его хранит канонический кадр, и перепутать порядок
/// каналов здесь физически негде.
/// </remarks>
public sealed class OwnModelMinimapRecognizer : IDisposable
{
    /// <summary>Сторона рамки подтверждённого значка — как у штатного конвейера.</summary>
    private const int MarkerSide = 25;

    private const int BytesPerPixel = 4;

    /// <summary>
    /// Окно подавления соседей: клетка остаётся вершиной, только если равна
    /// максимуму окрестности. Так два близких значка дают две вершины, а один
    /// значок — одну.
    /// </summary>
    private const int SuppressionWindow = 3;

    /// <summary>Плоскую вершину параболой не уточнить: знаменатель обращается в ноль.</summary>
    private const double FlatPeakEpsilon = 1e-6;

    /// <summary>
    /// Столько же, сколько берёт штатный конвейер: распознавание делит машину
    /// с игрой.
    /// </summary>
    private static int DefaultInferenceThreads =>
        DefaultMinimapFrameAnalyzer.DefaultDegreeOfParallelism;

    private readonly OwnModelSetup _setup;
    private readonly InferenceSession _detector;
    private readonly InferenceSession _identity;
    private readonly string _detectorInput;
    private readonly string _identityInput;
    private readonly byte[] _canonicalFrame;
    private readonly DenseTensor<byte> _frameTensor;
    private readonly int _mapSide;
    private bool _isDisposed;

    /// <param name="maximumDegreeOfParallelism">
    /// Сколько потоков разрешено инференсу. Без явного ограничения ONNX Runtime
    /// создаёт собственный пул по числу ядер и о настройке приложения не знает,
    /// поэтому обещание оставить процессор игре не выполняется.
    /// </param>
    public OwnModelMinimapRecognizer(string modelDirectory, int? maximumDegreeOfParallelism = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelDirectory);
        _setup = OwnModelSetup.Load(Path.Combine(modelDirectory, OwnModelFrameAnalyzer.SetupFileName));

        var threads = maximumDegreeOfParallelism ?? DefaultInferenceThreads;
        _detector = OpenSession(Path.Combine(modelDirectory, _setup.DetectorFile), threads);
        _identity = OpenSession(Path.Combine(modelDirectory, _setup.IdentityFile), threads);
        _detectorInput = _detector.InputMetadata.Keys.First();
        _identityInput = _identity.InputMetadata.Keys.First();

        _mapSide = _setup.FrameSide / _setup.Stride;
        _canonicalFrame = new byte[_setup.FrameSide * _setup.FrameSide * BytesPerPixel];
        _frameTensor = new DenseTensor<byte>([1, _setup.FrameSide, _setup.FrameSide, BytesPerPixel]);
    }

    public OwnModelSetup Setup => _setup;

    private static InferenceSession OpenSession(string path, int threads)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Файл модели не найден: {path}");
        }

        // Одна сессия на весь прогон: создание сессии стоит дороже самого инференса.
        using var options = new SessionOptions
        {
            IntraOpNumThreads = threads,
            // Кадр обрабатывается по одному, параллелить нечего между собой.
            InterOpNumThreads = 1,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };
        // Между кадрами рабочие потоки по умолчанию крутятся в ожидании вместо
        // того, чтобы уснуть. Кадры приходят раз в 125 мс, а рядом работает игра:
        // несколько миллисекунд задержки дешевле отнятого у неё ядра.
        options.AddSessionConfigEntry("session.intra_op.allow_spinning", "0");
        return new InferenceSession(path, options);
    }

    /// <summary>
    /// Разбирает канонический кадр целиком. Состав матча ограничивает выбор: сеть
    /// знает больше сотни чемпионов, но в матче их десять, и посторонние имена не
    /// подавляются, а просто не рассматриваются — это ровно то, что даёт знание
    /// состава из API.
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
        if (roster.Count == 0)
        {
            return [];
        }

        FillFrame(canonicalFrame, width, height, stride);
        var centers = FindCenters();
        if (centers.Count == 0)
        {
            return [];
        }

        var named = NameCenters(centers, roster);
        return KeepOnePerChampion(named, roster);
    }

    /// <summary>
    /// Приводит кадр к каноническому размеру и плотной укладке без зазора в строке.
    /// Дальше пиксели уходят в сеть как есть: преобразование живёт внутри графа.
    /// </summary>
    private void FillFrame(ReadOnlySpan<byte> canonicalFrame, int width, int height, int stride)
    {
        MinimapFrameNormalizer.Normalize(
            canonicalFrame,
            width,
            height,
            stride,
            _canonicalFrame,
            _setup.FrameSide,
            _setup.FrameSide,
            _setup.FrameSide * BytesPerPixel);
        _canonicalFrame.CopyTo(_frameTensor.Buffer.Span);
    }

    /// <summary>Карта центров → вершины в канонических пикселях, от уверенных к слабым.</summary>
    private List<Center> FindCenters()
    {
        using var results = _detector.Run(
            [NamedOnnxValue.CreateFromTensor(_detectorInput, _frameTensor)]);
        var map = results[0].AsTensor<float>();
        if (map.Length != _mapSide * _mapSide)
        {
            throw new InvalidDataException(
                $"Ожидалась карта центров {_mapSide}×{_mapSide}, получено значений: {map.Length}.");
        }

        var plane = map.ToArray();
        var found = new List<Center>();
        for (var gy = 0; gy < _mapSide; gy++)
        {
            for (var gx = 0; gx < _mapSide; gx++)
            {
                var value = plane[(gy * _mapSide) + gx];
                if (value < _setup.PeakThreshold || !IsLocalMaximum(plane, gy, gx))
                {
                    continue;
                }

                found.Add(new Center(
                    (gx + ParabolicOffset(plane, gy, gx, alongX: true)) * _setup.Stride,
                    (gy + ParabolicOffset(plane, gy, gx, alongX: false)) * _setup.Stride,
                    value));
            }
        }

        found.Sort(static (left, right) => right.Confidence.CompareTo(left.Confidence));
        return found;
    }

    private bool IsLocalMaximum(float[] plane, int gy, int gx)
    {
        const int reach = SuppressionWindow / 2;
        var value = plane[(gy * _mapSide) + gx];
        for (var dy = -reach; dy <= reach; dy++)
        {
            var y = gy + dy;
            if (y < 0 || y >= _mapSide)
            {
                continue;
            }

            for (var dx = -reach; dx <= reach; dx++)
            {
                var x = gx + dx;
                if (x >= 0 && x < _mapSide && plane[(y * _mapSide) + x] > value)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Субпиксельная поправка к клетке по вершине параболы через три значения.
    /// Клетка карты — это <see cref="OwnModelSetup.Stride"/> канонических пикселей,
    /// поэтому округление до клетки само по себе даёт ошибку до половины шага.
    /// Соседние значения бугра несут долю пикселя и восстанавливают её.
    /// </summary>
    private double ParabolicOffset(float[] plane, int gy, int gx, bool alongX)
    {
        var position = alongX ? gx : gy;
        if (position == 0 || position == _mapSide - 1)
        {
            return 0;
        }

        var step = alongX ? 1 : _mapSide;
        var index = (gy * _mapSide) + gx;
        double before = plane[index - step];
        double center = plane[index];
        double after = plane[index + step];

        var denominator = before - (2 * center) + after;
        if (Math.Abs(denominator) < FlatPeakEpsilon)
        {
            return 0;
        }

        // Вершина дальше половины клетки означала бы, что максимум не здесь.
        return Math.Clamp(0.5 * (before - after) / denominator, -0.5, 0.5);
    }

    /// <summary>Каждой вершине — имя чемпиона либо отказ, одним прогоном сети.</summary>
    private List<Named> NameCenters(
        List<Center> centers,
        IReadOnlyList<MinimapChampionReference> roster)
    {
        var side = _setup.CropSide;
        var half = side / 2;
        var places = new List<Center>(centers.Count);
        foreach (var center in centers)
        {
            // Округление к чётному, а не «от нуля». Это не вкус: образец на Python
            // округляет именно так, и на ровной половине пикселя вырез иначе
            // уехал бы на единицу. Расхождение редкое, тихое и ровно того сорта,
            // ради которого образец и заведён.
            var left = (int)Math.Round(center.X, MidpointRounding.ToEven) - half;
            var top = (int)Math.Round(center.Y, MidpointRounding.ToEven) - half;
            if (left < 0 || top < 0 ||
                left + side > _setup.FrameSide || top + side > _setup.FrameSide)
            {
                continue;
            }

            places.Add(center with { Left = left, Top = top });
        }

        var named = new List<Named>(places.Count);
        if (places.Count == 0)
        {
            return named;
        }

        var crops = new DenseTensor<byte>([places.Count, side, side, BytesPerPixel]);
        var destination = crops.Buffer.Span;
        var rowBytes = side * BytesPerPixel;
        for (var index = 0; index < places.Count; index++)
        {
            var place = places[index];
            for (var row = 0; row < side; row++)
            {
                var source = (((place.Top + row) * _setup.FrameSide) + place.Left) * BytesPerPixel;
                _canonicalFrame.AsSpan(source, rowBytes)
                    .CopyTo(destination.Slice(((index * side) + row) * rowBytes, rowBytes));
            }
        }

        using var results = _identity.Run(
            [NamedOnnxValue.CreateFromTensor(_identityInput, crops)]);
        var scores = results[0].AsTensor<float>().ToArray();
        var classes = _setup.Vocabulary.Count;
        var allowed = AllowedClasses(roster);

        for (var index = 0; index < places.Count; index++)
        {
            var offset = index * classes;
            var best = _setup.RejectIndex;
            foreach (var candidate in allowed)
            {
                if (scores[offset + candidate] > scores[offset + best])
                {
                    best = candidate;
                }
            }

            named.Add(new Named(places[index], _setup.Vocabulary[best], scores[offset + best]));
        }

        return named;
    }

    /// <summary>
    /// Классы, из которых разрешено выбирать: отказ плюс состав матча. Прочие не
    /// подавляются, а просто не рассматриваются.
    /// </summary>
    private int[] AllowedClasses(IReadOnlyList<MinimapChampionReference> roster)
    {
        var wanted = new HashSet<string>(
            roster.Select(static reference => reference.ChampionId),
            StringComparer.OrdinalIgnoreCase);
        var allowed = new List<int> { _setup.RejectIndex };
        for (var index = 0; index < _setup.Vocabulary.Count; index++)
        {
            if (index != _setup.RejectIndex && wanted.Contains(_setup.Vocabulary[index]))
            {
                allowed.Add(index);
            }
        }

        return [.. allowed];
    }

    /// <summary>
    /// Одно имя — один чемпион: из нескольких срабатываний с одним именем остаётся
    /// самое уверенное. Чемпион не может стоять на карте дважды, состав матча —
    /// десять разных героев. Это правило самой игры, а не уловка ради чисел.
    /// </summary>
    private MinimapChampionMarkerCandidate[] KeepOnePerChampion(
        List<Named> named,
        IReadOnlyList<MinimapChampionReference> roster)
    {
        var sides = roster.ToDictionary(
            static reference => reference.ChampionId,
            static reference => reference.Affiliation,
            StringComparer.OrdinalIgnoreCase);

        var bestFor = new Dictionary<string, Named>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in named)
        {
            if (string.Equals(entry.ChampionId, _setup.RejectName, StringComparison.Ordinal))
            {
                continue;
            }

            if (!bestFor.TryGetValue(entry.ChampionId, out var current) ||
                entry.Confidence > current.Confidence)
            {
                bestFor[entry.ChampionId] = entry;
            }
        }

        var limit = _setup.FrameSide - MarkerSide;
        var candidates = new List<MinimapChampionMarkerCandidate>(bestFor.Count);
        foreach (var entry in bestFor.Values)
        {
            var affiliation = sides.TryGetValue(entry.ChampionId, out var side)
                ? side
                : MinimapMarkerAffiliation.Unknown;
            var confidence = Math.Clamp(entry.Confidence, 0, 1);
            candidates.Add(new MinimapChampionMarkerCandidate(
                Math.Clamp((int)Math.Round(entry.Center.X) - (MarkerSide / 2), 0, limit),
                Math.Clamp((int)Math.Round(entry.Center.Y) - (MarkerSide / 2), 0, limit),
                MarkerSide,
                MarkerSide,
                affiliation,
                Math.Clamp(entry.Center.Confidence, 0, 1),
                entry.ChampionId,
                confidence,
                affiliation));
        }

        return [.. candidates.OrderByDescending(static candidate => candidate.IdentityConfidence)];
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _detector.Dispose();
        _identity.Dispose();
    }

    /// <summary>Вершина карты центров в канонических пикселях и её вырез.</summary>
    private readonly record struct Center(double X, double Y, float Confidence, int Left = 0, int Top = 0);

    /// <summary>Вершина с выбранным именем и уверенностью в нём.</summary>
    private readonly record struct Named(Center Center, string ChampionId, float Confidence);
}
