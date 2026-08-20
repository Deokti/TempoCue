using OpenCvSharp;

namespace TempoCue.Vision;

/// <summary>
/// Сопоставляет цветные портреты средствами OpenCV и не допускает повторного
/// назначения одного участника нескольким областям одного кадра.
/// </summary>
internal sealed class OpenCvMinimapChampionRecognizer : IMinimapChampionRecognizer
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

    public MinimapChampionRecognitionResult Recognize(
        ReadOnlyMemory<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<MinimapChampionReference> references)
    {
        ValidateImage(pixels.Span, width, height, stride);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(references);
        if (candidates.Count == 0 || references.Count == 0)
        {
            return new MinimapChampionRecognitionResult([], 0);
        }

        var sourcePixels = pixels.ToArray();
        using var sourceBgra = Mat.FromPixelData(
            height,
            width,
            MatType.CV_8UC4,
            sourcePixels,
            stride);
        using var sourceBgr = new Mat();
        Cv2.CvtColor(sourceBgra, sourceBgr, ColorConversionCodes.BGRA2BGR);

        var preparedReferences = references
            .Select(PreparedReference.Create)
            .ToArray();
        try
        {
            return MatchCandidates(sourceBgr, candidates, preparedReferences);
        }
        finally
        {
            foreach (var reference in preparedReferences)
            {
                reference.Dispose();
            }
        }
    }

    private static MinimapChampionRecognitionResult MatchCandidates(
        Mat source,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<PreparedReference> references)
    {
        var proposedMatches = new List<ProposedMatch>(candidates.Count);
        var maximumObservedSimilarity = -1d;
        for (var candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
        {
            var candidate = candidates[candidateIndex];
            PreparedReference? bestReference = null;
            var bestSimilarity = -1d;
            var secondSimilarity = -1d;

            foreach (var reference in references)
            {
                if (reference.Reference.Affiliation != candidate.Affiliation)
                {
                    continue;
                }

                var similarity = FindBestSimilarity(source, candidate, reference.Bgr);
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

    private static double FindBestSimilarity(
        Mat source,
        MinimapChampionMarkerCandidate candidate,
        Mat reference)
    {
        var bestSimilarity = -1d;
        var centerX = candidate.X + ((candidate.Width - 1) / 2d);
        var centerY = candidate.Y + ((candidate.Height - 1) / 2d);

        foreach (var sourceSide in SourceSides)
        {
            var templateSide = Math.Max(
                7,
                (int)Math.Round(
                    sourceSide * ReferenceCropSide /
                    (double)MinimapChampionReference.ImageSide));
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

            using var resizedReference = new Mat();
            Cv2.Resize(
                reference,
                resizedReference,
                new Size(templateSide, templateSide),
                0,
                0,
                templateSide < reference.Width
                    ? InterpolationFlags.Area
                    : InterpolationFlags.Linear);
            using var searchRegion = new Mat(
                source,
                new Rect(searchX, searchY, searchWidth, searchHeight));
            using var structureMap = new Mat();
            Cv2.MatchTemplate(
                searchRegion,
                resizedReference,
                structureMap,
                TemplateMatchModes.CCoeffNormed);
            using var colorDifferenceMap = new Mat();
            Cv2.MatchTemplate(
                searchRegion,
                resizedReference,
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
                ChampionId = match.Reference.ChampionId,
                IdentityConfidence = match.Similarity,
                Confidence = (match.Candidate.Confidence + match.Similarity) / 2
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

    private sealed class PreparedReference : IDisposable
    {
        private PreparedReference(MinimapChampionReference reference, Mat bgr)
        {
            Reference = reference;
            Bgr = bgr;
        }

        public MinimapChampionReference Reference { get; }

        public Mat Bgr { get; }

        public static PreparedReference Create(MinimapChampionReference reference)
        {
            var pixels = reference.BgraPixels.ToArray();
            using var bgra = Mat.FromPixelData(
                MinimapChampionReference.ImageSide,
                MinimapChampionReference.ImageSide,
                MatType.CV_8UC4,
                pixels);
            using var fullBgr = new Mat();
            Cv2.CvtColor(bgra, fullBgr, ColorConversionCodes.BGRA2BGR);
            using var center = new Mat(
                fullBgr,
                new Rect(
                    ReferenceCropOffset,
                    ReferenceCropOffset,
                    ReferenceCropSide,
                    ReferenceCropSide));
            return new PreparedReference(reference, center.Clone());
        }

        public void Dispose() => Bgr.Dispose();
    }

    private sealed record ProposedMatch(
        int CandidateIndex,
        MinimapChampionMarkerCandidate Candidate,
        MinimapChampionReference Reference,
        double Similarity);
}
