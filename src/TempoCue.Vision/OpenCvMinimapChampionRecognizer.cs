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
    private const int DuplicateCenterDistance = 10;
    private const int ReferenceCropSide = 11;
    private const int ReferenceCropOffset =
        (MinimapChampionReference.ImageSide - ReferenceCropSide) / 2;
    private const double StructureWeight = 0.4;
    private const double ColorWeight = 0.6;
    private const double MinimumIdentityConfidence = 0.75;
    private const double MinimumIdentityMargin = 0.04;
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

    private static MinimapChampionRecognitionResult MatchCandidates(
        Mat source,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<PreparedReference> references,
        IReadOnlyList<Mat[]> negativeTemplates)
    {
        var referenceGroups = references
            .GroupBy(
                static reference => reference.Reference.ChampionId,
                StringComparer.OrdinalIgnoreCase)
            .Select(static group => group
                .OrderBy(static reference => reference.Reference.Affiliation)
                .ToArray())
            .ToArray();
        var proposedMatches = new List<ProposedMatch>(candidates.Count);
        var maximumObservedSimilarity = -1d;
        for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            PreparedReference? bestReference = null;
            var bestSimilarity = -1d;
            var secondSimilarity = -1d;

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
                if (similarity > bestSimilarity)
                {
                    secondSimilarity = bestSimilarity;
                    bestSimilarity = similarity;
                    bestReference = reference;
                }
                else if (similarity > secondSimilarity)
                {
                    secondSimilarity = similarity;
                }
            }

            if (bestReference is null)
            {
                continue;
            }

            maximumObservedSimilarity = Math.Max(maximumObservedSimilarity, bestSimilarity);
            if (bestSimilarity < MinimumIdentityConfidence ||
                bestSimilarity - secondSimilarity < MinimumIdentityMargin)
            {
                continue;
            }

            // Башни, строения базы, метки местности и миньоны статичны и проходят
            // временную стабилизацию, поэтому отсекаются здесь: область, похожая на
            // такой объект не слабее, чем на лучший портрет, не является чемпионом.
            if (MatchesNegativeReference(
                    source,
                    candidate,
                    negativeTemplates,
                    bestSimilarity))
            {
                continue;
            }

            proposedMatches.Add(new ProposedMatch(
                candidateIndex,
                candidate,
                bestReference.Reference,
                bestSimilarity));
        }

        var selected = SelectUniqueMatches(proposedMatches);
        return new MinimapChampionRecognitionResult(
            selected,
            maximumObservedSimilarity < 0 ? 0 : maximumObservedSimilarity);
    }

    private static bool MatchesNegativeReference(
        Mat source,
        MinimapChampionMarkerCandidate candidate,
        IReadOnlyList<Mat[]> negativeTemplates,
        double championSimilarity)
    {
        foreach (var negative in negativeTemplates)
        {
            if (FindBestSimilarity(source, candidate, negative) >= championSimilarity)
            {
                return true;
            }
        }

        return false;
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

    private static MinimapChampionMarkerCandidate[] SelectUniqueMatches(
        IReadOnlyList<ProposedMatch> proposedMatches)
    {
        var selected = new List<ProposedMatch>(proposedMatches.Count);
        var assignedParticipants = new HashSet<(string ChampionId, MinimapMarkerAffiliation Affiliation)>();

        foreach (var proposal in proposedMatches
                     .OrderByDescending(match => match.Similarity)
                     .ThenBy(match => match.Candidate.Y)
                     .ThenBy(match => match.Candidate.X)
                     .ThenBy(match => match.Reference.ChampionId, StringComparer.Ordinal))
        {
            var participant = (
                proposal.Reference.ChampionId,
                proposal.Reference.Affiliation);
            if (assignedParticipants.Contains(participant) ||
                selected.Any(existing => AreDuplicateRegions(
                    existing.Candidate,
                    proposal.Candidate)))
            {
                continue;
            }

            assignedParticipants.Add(participant);
            selected.Add(proposal);
        }

        return selected
            .OrderBy(match => match.CandidateIndex)
            .Select(match => match.Candidate with
            {
                Affiliation = match.Reference.Affiliation,
                ChampionId = match.Reference.ChampionId,
                IdentityConfidence = match.Similarity,
                Confidence = (match.Candidate.Confidence + match.Similarity) / 2,
                ContourAffiliation = match.Candidate.Affiliation
            })
            .ToArray();
    }

    private static bool AreDuplicateRegions(
        MinimapChampionMarkerCandidate first,
        MinimapChampionMarkerCandidate second)
    {
        var firstCenterX = first.X + (first.Width / 2d);
        var firstCenterY = first.Y + (first.Height / 2d);
        var secondCenterX = second.X + (second.Width / 2d);
        var secondCenterY = second.Y + (second.Height / 2d);
        var deltaX = firstCenterX - secondCenterX;
        var deltaY = firstCenterY - secondCenterY;
        return (deltaX * deltaX) + (deltaY * deltaY) <=
            DuplicateCenterDistance * DuplicateCenterDistance;
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

    private sealed record ProposedMatch(
        int CandidateIndex,
        MinimapChampionMarkerCandidate Candidate,
        MinimapChampionReference Reference,
        double Similarity);
}
