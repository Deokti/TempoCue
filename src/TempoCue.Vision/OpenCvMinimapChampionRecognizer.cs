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

    private IReadOnlyList<MinimapChampionReference>? _preparedSource;
    private PreparedReference[] _preparedReferences = [];
    private Mat[][]? _preparedNegatives;
    private bool _isDisposed;

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
            GetPreparedNegatives());
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
            out _).ToArray();
    }

    private static MinimapChampionRecognitionResult MatchCandidates(
        Mat source,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<PreparedReference> references,
        IReadOnlyList<Mat[]> negativeTemplates)
    {
        var candidateMatches = ComputeCandidateScores(
            source,
            candidates,
            references,
            negativeTemplates,
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
        var candidateMatches = new List<MinimapRosterCandidateScore>(candidates.Count);
        maximumObservedSimilarity = -1d;
        for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            var referenceMatches = new List<MinimapRosterReferenceScore>(referenceGroups.Length);

            foreach (var referenceGroup in referenceGroups)
            {
                // Портрет определяет участника независимо от шумного цвета контура.
                // При зеркальном чемпионе сторона контура выбирает один из двух
                // одинаковых эталонов уже после определения личности.
                var reference = referenceGroup.FirstOrDefault(reference =>
                        reference.Reference.Affiliation == candidate.Affiliation) ??
                    referenceGroup[0];

                var similarity = FindBestSimilarity(
                    source,
                    candidate,
                    reference.ScaledTemplates);
                referenceMatches.Add(new MinimapRosterReferenceScore(
                    reference.Reference,
                    similarity));
            }

            if (referenceMatches.Count == 0)
            {
                continue;
            }

            var bestSimilarity = referenceMatches.Max(static match => match.Similarity);
            maximumObservedSimilarity = Math.Max(maximumObservedSimilarity, bestSimilarity);
            // Башни, строения базы, метки местности и миньоны статичны и проходят
            // временную стабилизацию. Максимальное сходство с ними сохраняется,
            // чтобы при выборе каждого доступного участника отрицательный эталон
            // сравнивался именно с его баллом, а не только с первоначальным лидером.
            var negativeSimilarity = FindBestNegativeSimilarity(
                source,
                candidate,
                negativeTemplates);
            candidateMatches.Add(new MinimapRosterCandidateScore(
                candidateIndex,
                candidate,
                referenceMatches
                    .OrderByDescending(static match => match.Similarity)
                    .ThenBy(static match => match.Reference.ChampionId, StringComparer.Ordinal)
                    .ToArray(),
                negativeSimilarity));
        }

        return candidateMatches;
    }

    private static double FindBestNegativeSimilarity(
        Mat source,
        MinimapChampionMarkerCandidate candidate,
        IReadOnlyList<Mat[]> negativeTemplates)
    {
        var bestSimilarity = -1d;
        foreach (var negative in negativeTemplates)
        {
            bestSimilarity = Math.Max(
                bestSimilarity,
                FindBestSimilarity(source, candidate, negative));
        }

        return bestSimilarity;
    }

    private static double FindBestSimilarity(
        Mat source,
        MinimapChampionMarkerCandidate candidate,
        IReadOnlyList<Mat> scaledTemplates)
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
            using var structureMap = new Mat();
            Cv2.MatchTemplate(
                searchRegion,
                template,
                structureMap,
                TemplateMatchModes.CCoeffNormed);
            using var colorDifferenceMap = new Mat();
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
        var bestSimilarity = -1d;
        var rows = structureMap.Rows;
        var columns = structureMap.Cols;
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < columns; x++)
            {
                // CCOEFF сравнивает рисунок, а SQDIFF сохраняет абсолютные
                // различия цвета, которые отделяют портрет от похожей башни.
                var structureSimilarity =
                    (Math.Clamp(structureMap.At<float>(y, x), -1, 1) + 1) / 2;
                var colorSimilarity = 1 - Math.Clamp(
                    colorDifferenceMap.At<float>(y, x),
                    0,
                    1);
                var combinedSimilarity =
                    (structureSimilarity * StructureWeight) +
                    (colorSimilarity * ColorWeight);
                bestSimilarity = Math.Max(bestSimilarity, combinedSimilarity);
            }
        }

        return bestSimilarity;
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
