using System.Runtime.InteropServices;
using OpenCvSharp;

namespace TempoCue.Vision;

/// <summary>
/// Сопоставляет цветные портреты средствами OpenCV и не допускает повторного
/// назначения одного участника нескольким областям одного кадра.
/// </summary>
internal sealed class OpenCvMinimapChampionRecognizer : IMinimapChampionRecognizer, IDisposable
{
    private const int BytesPerPixel = 4;
    private const int AlignmentRadius = 8;
    private const int ReferenceCropSide = 11;
    private const int ReferenceCropOffset =
        (MinimapChampionReference.ImageSide - ReferenceCropSide) / 2;
    private const double StructureWeight = 0.4;
    private const double ColorWeight = 0.6;
    private static readonly int[] SourceSides = [13, 15, 17, 19, 21];
    private static readonly int[] TemplateSides = SourceSides
        .Select(static sourceSide => Math.Max(
            7,
            (int)Math.Round(
                sourceSide * ReferenceCropSide /
                (double)MinimapChampionReference.ImageSide)))
        .ToArray();

    private readonly int _maximumDegreeOfParallelism;
    private IReadOnlyList<MinimapChampionReference>? _preparedSource;
    private PreparedReference[] _preparedReferences = [];
    private Mat[][]? _preparedNegatives;
    private bool _isDisposed;

    public OpenCvMinimapChampionRecognizer()
        : this(DefaultDegreeOfParallelism)
    {
    }

    /// <param name="maximumDegreeOfParallelism">
    /// Сколько областей кадра сравнивается одновременно. Области независимы, и
    /// результат от их числа не зависит — меняется только время разбора.
    /// </param>
    public OpenCvMinimapChampionRecognizer(int maximumDegreeOfParallelism)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDegreeOfParallelism);
        _maximumDegreeOfParallelism = maximumDegreeOfParallelism;
    }

    /// <summary>
    /// Четверть логических процессоров, но не меньше двух. Значение выбрано по
    /// замеру, а не по вкусу: подтверждение видимости требует двух наблюдений с
    /// разрывом не более 600 мс, поэтому разбор кадра обязан укладываться в 300 мс.
    /// На одном потоке кадр стоит около 415 мс — участник не подтверждается вовсе,
    /// на двух около 230 мс, на четырёх около 145 мс. Четверть даёт запас и
    /// оставляет игре большую часть машины.
    /// </summary>
    public static int DefaultDegreeOfParallelism
    {
        get
        {
            var processorCount = Math.Max(1, Environment.ProcessorCount);
            return Math.Min(processorCount, Math.Max(2, processorCount / 4));
        }
    }

    public MinimapChampionRecognitionResult Recognize(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<MinimapChampionReference> references)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ValidateImage(pixels.Span, width, height, stride);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(references);
        if (candidates.Count == 0 || references.Count == 0)
        {
            return new MinimapChampionRecognitionResult([], 0);
        }

        using var sourceBgra = CreateBgraMat(pixels, width, height, stride);
        using var sourceBgr = new Mat();
        Cv2.CvtColor(sourceBgra, sourceBgr, ColorConversionCodes.BGRA2BGR);

        return MatchCandidates(
            sourceBgr,
            candidates,
            GetPreparedReferences(references),
            GetPreparedNegatives(),
            _maximumDegreeOfParallelism);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        DisposePreparedReferences();
        if (_preparedNegatives is { } negatives)
        {
            foreach (var templates in negatives)
            {
                foreach (var template in templates)
                {
                    template.Dispose();
                }
            }

            _preparedNegatives = null;
        }
    }

    private static Mat CreateBgraMat(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride)
    {
        // Кадр оборачивается без копирования: Mat живёт только внутри синхронного
        // прохода, а копия канонического кадра на каждом обработанном кадре
        // засоряла кучу больших объектов при длительном матче.
        var buffer = MemoryMarshal.TryGetArray(pixels, out var segment) &&
            segment.Offset == 0 &&
            segment.Array is { } array
                ? array
                : pixels.ToArray();
        return Mat.FromPixelData(height, width, MatType.CV_8UC4, buffer, stride);
    }

    private PreparedReference[] GetPreparedReferences(
        IReadOnlyList<MinimapChampionReference> references)
    {
        if (ReferenceEquals(_preparedSource, references))
        {
            return _preparedReferences;
        }

        DisposePreparedReferences();
        // Эталоны меняются только вместе с составом матча, поэтому конвертация и
        // масштабирование выполняются один раз, а не для каждой пары кадр × кандидат.
        _preparedReferences = references.Select(PreparedReference.Create).ToArray();
        _preparedSource = references;
        return _preparedReferences;
    }

    private void DisposePreparedReferences()
    {
        foreach (var reference in _preparedReferences)
        {
            reference.Dispose();
        }

        _preparedReferences = [];
        _preparedSource = null;
    }

    private Mat[][] GetPreparedNegatives() =>
        _preparedNegatives ??= MinimapNegativeReferenceCatalog.LoadBgraTemplates()
            .Select(static template => CreateScaledTemplates(template))
            .ToArray();

    /// <summary>
    /// Диагностический срез сырых баллов каждой области до назначения состава.
    /// Используется offline-инструментами replay для калибровки правил.
    /// </summary>
    internal MinimapRosterCandidateScore[] ScoreCandidates(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<MinimapChampionReference> references)
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        ValidateImage(pixels.Span, width, height, stride);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(references);
        if (candidates.Count == 0 || references.Count == 0)
        {
            return [];
        }

        using var sourceBgra = CreateBgraMat(pixels, width, height, stride);
        using var sourceBgr = new Mat();
        Cv2.CvtColor(sourceBgra, sourceBgr, ColorConversionCodes.BGRA2BGR);

        return ComputeCandidateScores(
            sourceBgr,
            candidates,
            GetPreparedReferences(references),
            GetPreparedNegatives(),
            _maximumDegreeOfParallelism,
            out _).ToArray();
    }

    private static MinimapChampionRecognitionResult MatchCandidates(
        Mat source,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<PreparedReference> references,
        IReadOnlyList<Mat[]> negativeTemplates,
        int maximumDegreeOfParallelism)
    {
        var candidateMatches = ComputeCandidateScores(
            source,
            candidates,
            references,
            negativeTemplates,
            maximumDegreeOfParallelism,
            out var maximumObservedSimilarity);
        var selected = MinimapRosterAssignment.Select(candidateMatches);
        return new MinimapChampionRecognitionResult(
            selected,
            maximumObservedSimilarity < 0 ? 0 : maximumObservedSimilarity);
    }

    private static List<MinimapRosterCandidateScore> ComputeCandidateScores(
        Mat source,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<PreparedReference> references,
        IReadOnlyList<Mat[]> negativeTemplates,
        int maximumDegreeOfParallelism,
        out double maximumObservedSimilarity)
    {
        var referenceGroups = references
            .GroupBy(
                static reference => reference.Reference.ChampionId,
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderBy(static reference => reference.Reference.Affiliation)
                .ToArray())
            .ToArray();
        // Области кадра независимы друг от друга, поэтому сравниваются одновременно,
        // а результат складывается по индексу области. Порядок кандидатов обязан
        // остаться прежним: назначение состава разрешает равные баллы по нему.
        var scores = new MinimapRosterCandidateScore?[candidates.Count];
        var bestSimilarities = new double[candidates.Count];
        Parallel.For(
            0,
            candidates.Count,
            new ParallelOptions { MaxDegreeOfParallelism = maximumDegreeOfParallelism },
            MatchScratch.Create,
            (candidateIndex, _, scratch) =>
            {
                var candidate = candidates[candidateIndex];
                var referenceMatches =
                    new MinimapRosterReferenceScore[referenceGroups.Length];
                for (var groupIndex = 0; groupIndex < referenceGroups.Length; groupIndex++)
                {
                    // Портрет определяет участника независимо от шумного цвета контура.
                    // При зеркальном чемпионе сторона контура выбирает один из двух
                    // одинаковых эталонов уже после определения личности.
                    var referenceGroup = referenceGroups[groupIndex];
                    var reference = referenceGroup.FirstOrDefault(reference =>
                            reference.Reference.Affiliation == candidate.Affiliation) ??
                        referenceGroup[0];

                    referenceMatches[groupIndex] = new MinimapRosterReferenceScore(
                        reference.Reference,
                        FindBestSimilarity(
                            source,
                            candidate,
                            reference.ScaledTemplates,
                            scratch));
                }

                if (referenceMatches.Length == 0)
                {
                    return scratch;
                }

                bestSimilarities[candidateIndex] =
                    referenceMatches.Max(static match => match.Similarity);
                // Башни, строения базы, метки местности и миньоны статичны и проходят
                // временную стабилизацию. Максимальное сходство с ними сохраняется,
                // чтобы при выборе каждого доступного участника отрицательный эталон
                // сравнивался именно с его баллом, а не только с первоначальным лидером.
                var negativeSimilarity = FindBestNegativeSimilarity(
                    source,
                    candidate,
                    negativeTemplates,
                    scratch);
                scores[candidateIndex] = new MinimapRosterCandidateScore(
                    candidateIndex,
                    candidate,
                    referenceMatches
                        .OrderByDescending(static match => match.Similarity)
                        .ThenBy(static match => match.Reference.ChampionId, StringComparer.Ordinal)
                        .ToArray(),
                    negativeSimilarity);
                return scratch;
            },
            static scratch => scratch.Dispose());

        maximumObservedSimilarity = -1d;
        var candidateMatches = new List<MinimapRosterCandidateScore>(candidates.Count);
        for (var candidateIndex = 0; candidateIndex < scores.Length; candidateIndex++)
        {
            if (scores[candidateIndex] is not { } score)
            {
                continue;
            }

            maximumObservedSimilarity = Math.Max(
                maximumObservedSimilarity,
                bestSimilarities[candidateIndex]);
            candidateMatches.Add(score);
        }

        return candidateMatches;
    }

    private static double FindBestNegativeSimilarity(
        Mat source,
        MinimapChampionMarkerCandidate candidate,
        IReadOnlyList<Mat[]> negativeTemplates,
        MatchScratch scratch)
    {
        var bestSimilarity = -1d;
        foreach (var negative in negativeTemplates)
        {
            bestSimilarity = Math.Max(
                bestSimilarity,
                FindBestSimilarity(source, candidate, negative, scratch));
        }

        return bestSimilarity;
    }

    private static double FindBestSimilarity(
        Mat source,
        MinimapChampionMarkerCandidate candidate,
        IReadOnlyList<Mat> scaledTemplates,
        MatchScratch scratch)
    {
        var bestSimilarity = -1d;
        var centerX = candidate.X + ((candidate.Width - 1) / 2d);
        var centerY = candidate.Y + ((candidate.Height - 1) / 2d);

        for (var scaleIndex = 0; scaleIndex < TemplateSides.Length; scaleIndex++)
        {
            var templateSide = TemplateSides[scaleIndex];
            var halfSide = (templateSide - 1) / 2d;
            var expectedX = (int)Math.Round(centerX - halfSide);
            var expectedY = (int)Math.Round(centerY - halfSide);
            var searchX = Math.Max(0, expectedX - AlignmentRadius);
            var searchY = Math.Max(0, expectedY - AlignmentRadius);
            var searchRight = Math.Min(
                source.Cols,
                expectedX + AlignmentRadius + templateSide);
            var searchBottom = Math.Min(
                source.Rows,
                expectedY + AlignmentRadius + templateSide);
            var searchWidth = searchRight - searchX;
            var searchHeight = searchBottom - searchY;
            if (searchWidth < templateSide || searchHeight < templateSide)
            {
                continue;
            }

            var template = scaledTemplates[scaleIndex];
            using var searchRegion = new Mat(
                source,
                new Rect(searchX, searchY, searchWidth, searchHeight));
            // Карты совпадений переиспользуются по индексу масштаба: их размер при
            // одном масштабе постоянен, поэтому буфер не перевыделяется.
            var structureMap = scratch.StructureMap(scaleIndex);
            Cv2.MatchTemplate(
                searchRegion,
                template,
                structureMap,
                TemplateMatchModes.CCoeffNormed);
            var colorDifferenceMap = scratch.ColorDifferenceMap(scaleIndex);
            Cv2.MatchTemplate(
                searchRegion,
                template,
                colorDifferenceMap,
                TemplateMatchModes.SqDiffNormed);
            bestSimilarity = Math.Max(
                bestSimilarity,
                FindBestCombinedSimilarity(structureMap, colorDifferenceMap));
        }

        return bestSimilarity;
    }

    private static double FindBestCombinedSimilarity(
        Mat structureMap,
        Mat colorDifferenceMap)
    {
        // Карты совпадений читаются одним куском. Обращение через Mat.At стоит
        // вызова нативной библиотеки на каждый элемент, а таких элементов за кадр
        // выходит больше двух миллионов — это была основная цена распознавания.
        if (structureMap.IsContinuous() && colorDifferenceMap.IsContinuous())
        {
            return FindBestCombinedSimilarity(
                structureMap.AsSpan<float>(),
                colorDifferenceMap.AsSpan<float>());
        }

        var bestSimilarity = -1d;
        var rows = structureMap.Rows;
        var columns = structureMap.Cols;
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                bestSimilarity = Math.Max(
                    bestSimilarity,
                    CombineSimilarity(
                        structureMap.At<float>(y, x),
                        colorDifferenceMap.At<float>(y, x)));
            }
        }

        return bestSimilarity;
    }

    private static double FindBestCombinedSimilarity(
        ReadOnlySpan<float> structure,
        ReadOnlySpan<float> colorDifference)
    {
        var bestSimilarity = -1d;
        var length = Math.Min(structure.Length, colorDifference.Length);
        for (var index = 0; index < length; index++)
        {
            bestSimilarity = Math.Max(
                bestSimilarity,
                CombineSimilarity(structure[index], colorDifference[index]));
        }

        return bestSimilarity;
    }

    /// <summary>
    /// CCOEFF сравнивает рисунок, а SQDIFF сохраняет абсолютные различия цвета,
    /// которые отделяют портрет от похожей башни.
    /// </summary>
    private static double CombineSimilarity(float structure, float colorDifference)
    {
        var structureSimilarity = (Math.Clamp(structure, -1, 1) + 1) / 2;
        var colorSimilarity = 1 - Math.Clamp(colorDifference, 0, 1);
        return (structureSimilarity * StructureWeight) +
            (colorSimilarity * ColorWeight);
    }

    private static void ValidateImage(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (stride < checked(width * BytesPerPixel))
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }

        if (pixels.Length < checked(stride * height))
        {
            throw new ArgumentException("Размер массива не соответствует изображению.", nameof(pixels));
        }
    }

    /// <summary>
    /// Готовит шаблоны BGR всех масштабов, выровненные по индексам с
    /// <see cref="TemplateSides"/>, из эталона 17 × 17 BGRA32.
    /// </summary>
    private static Mat[] CreateScaledTemplates(byte[] bgraPixels)
    {
        using var bgra = Mat.FromPixelData(
            MinimapChampionReference.ImageSide,
            MinimapChampionReference.ImageSide,
            MatType.CV_8UC4,
            bgraPixels);
        using var fullBgr = new Mat();
        Cv2.CvtColor(bgra, fullBgr, ColorConversionCodes.BGRA2BGR);
        using var center = new Mat(
            fullBgr,
            new Rect(
                ReferenceCropOffset,
                ReferenceCropOffset,
                ReferenceCropSide,
                ReferenceCropSide));
        var scaledTemplates = new Mat[TemplateSides.Length];
        try
        {
            for (var index = 0; index < TemplateSides.Length; index++)
            {
                var templateSide = TemplateSides[index];
                var scaled = new Mat();
                Cv2.Resize(
                    center,
                    scaled,
                    new Size(templateSide, templateSide),
                    0,
                    0,
                    templateSide < ReferenceCropSide
                        ? InterpolationFlags.Area
                        : InterpolationFlags.Linear);
                scaledTemplates[index] = scaled;
            }
        }
        catch
        {
            foreach (var scaled in scaledTemplates)
            {
                scaled?.Dispose();
            }

            throw;
        }

        return scaledTemplates;
    }

    /// <summary>
    /// Рабочие буферы одного потока сравнения. Карты совпадений живут по индексу
    /// масштаба, потому что размер карты определяется только им.
    /// </summary>
    private sealed class MatchScratch : IDisposable
    {
        private readonly Mat[] _structureMaps;
        private readonly Mat[] _colorDifferenceMaps;

        private MatchScratch(Mat[] structureMaps, Mat[] colorDifferenceMaps)
        {
            _structureMaps = structureMaps;
            _colorDifferenceMaps = colorDifferenceMaps;
        }

        public static MatchScratch Create() =>
            new(CreateMaps(), CreateMaps());

        public Mat StructureMap(int scaleIndex) => _structureMaps[scaleIndex];

        public Mat ColorDifferenceMap(int scaleIndex) => _colorDifferenceMaps[scaleIndex];

        public void Dispose()
        {
            foreach (var map in _structureMaps)
            {
                map.Dispose();
            }

            foreach (var map in _colorDifferenceMaps)
            {
                map.Dispose();
            }
        }

        private static Mat[] CreateMaps()
        {
            var maps = new Mat[TemplateSides.Length];
            for (var index = 0; index < maps.Length; index++)
            {
                maps[index] = new Mat();
            }

            return maps;
        }
    }

    private sealed class PreparedReference : IDisposable
    {
        private PreparedReference(MinimapChampionReference reference, Mat[] scaledTemplates)
        {
            Reference = reference;
            ScaledTemplates = scaledTemplates;
        }

        public MinimapChampionReference Reference { get; }

        /// <summary>Шаблоны BGR, выровненные по индексам с <see cref="TemplateSides"/>.</summary>
        public IReadOnlyList<Mat> ScaledTemplates { get; }

        public static PreparedReference Create(MinimapChampionReference reference) =>
            new(reference, CreateScaledTemplates(reference.BgraPixels.ToArray()));

        public void Dispose()
        {
            foreach (var template in ScaledTemplates)
            {
                template.Dispose();
            }
        }
    }

}
