namespace TempoCue.Vision;

/// <summary>
/// Подтверждает кандидатов сопоставлением с портретами участников активного матча.
/// </summary>
internal static class MinimapChampionIdentityMatcher
{
    private const int BytesPerPixel = 4;
    // Цветовой контур может представлять лишь дугу рамки портрета, поэтому центр
    // компоненты заметно смещён относительно центра самой иконки.
    private const int AlignmentRadius = 10;
    private const double MinimumIdentityConfidence = 0.48;
    private const double MinimumIdentityMargin = 0.06;
    private const double ComparisonRadiusSquared = 42.25;
    private static readonly int[] SourceSides = [13, 15, 17, 19, 21];

    public static MinimapChampionMarkerCandidate[] Match(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<MinimapChampionReference> references) =>
        Match(
            pixels,
            width,
            height,
            stride,
            candidates,
            references,
            out _);

    public static MinimapChampionMarkerCandidate[] Match(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        IReadOnlyList<MinimapChampionReference> references,
        out double maximumObservedSimilarity)
    {
        ValidateImage(pixels, width, height, stride);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(references);
        maximumObservedSimilarity = -1;
        if (candidates.Count == 0 || references.Count == 0)
        {
            return [];
        }

        var matches = new List<MinimapChampionMarkerCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            MinimapChampionReference? bestReference = null;
            var bestSimilarity = -1d;
            var secondSimilarity = -1d;
            foreach (var reference in references)
            {
                if (reference.Affiliation != candidate.Affiliation)
                {
                    continue;
                }

                var similarity = FindBestSimilarity(
                    pixels,
                    width,
                    height,
                    stride,
                    candidate,
                    reference);
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

            maximumObservedSimilarity = Math.Max(
                maximumObservedSimilarity,
                bestSimilarity);

            if (bestSimilarity < MinimumIdentityConfidence ||
                bestSimilarity - secondSimilarity < MinimumIdentityMargin)
            {
                continue;
            }

            matches.Add(candidate with
            {
                ChampionId = bestReference.ChampionId,
                IdentityConfidence = bestSimilarity,
                Confidence = (candidate.Confidence + bestSimilarity) / 2
            });
        }

        if (maximumObservedSimilarity < -0.999)
        {
            maximumObservedSimilarity = 0;
        }

        return matches.ToArray();
    }

    private static double FindBestSimilarity(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride,
        MinimapChampionMarkerCandidate candidate,
        MinimapChampionReference reference)
    {
        var bestSimilarity = -1d;
        var candidateCenterX = candidate.X + ((candidate.Width - 1) / 2d);
        var candidateCenterY = candidate.Y + ((candidate.Height - 1) / 2d);
        foreach (var sourceSide in SourceSides)
        {
            var sourceRadius = (sourceSide - 1) / 2d;
            for (var offsetY = -AlignmentRadius; offsetY <= AlignmentRadius; offsetY++)
            {
                for (var offsetX = -AlignmentRadius; offsetX <= AlignmentRadius; offsetX++)
                {
                    var startX = (int)Math.Round(candidateCenterX - sourceRadius) + offsetX;
                    var startY = (int)Math.Round(candidateCenterY - sourceRadius) + offsetY;
                    if (startX < 0 ||
                        startY < 0 ||
                        startX > width - sourceSide ||
                        startY > height - sourceSide)
                    {
                        continue;
                    }

                    bestSimilarity = Math.Max(
                        bestSimilarity,
                        CalculateNormalizedCorrelation(
                            pixels,
                            stride,
                            startX,
                            startY,
                            sourceSide,
                            reference.BgraPixels.Span));
                }
            }
        }

        return bestSimilarity;
    }

    private static double CalculateNormalizedCorrelation(
        ReadOnlySpan<byte> pixels,
        int stride,
        int startX,
        int startY,
        int sourceSide,
        ReadOnlySpan<byte> referencePixels)
    {
        var sampleCount = 0;
        double observedSum = 0;
        double referenceSum = 0;
        var center = (MinimapChampionReference.ImageSide - 1) / 2d;
        for (var y = 0; y < MinimapChampionReference.ImageSide; y++)
        {
            for (var x = 0; x < MinimapChampionReference.ImageSide; x++)
            {
                var deltaX = x - center;
                var deltaY = y - center;
                if ((deltaX * deltaX) + (deltaY * deltaY) > ComparisonRadiusSquared)
                {
                    continue;
                }

                var observedOffset = GetObservedOffset(
                    stride,
                    startX,
                    startY,
                    sourceSide,
                    x,
                    y);
                var referenceOffset = checked(
                    ((y * MinimapChampionReference.ImageSide) + x) * BytesPerPixel);
                observedSum += CalculateLuminance(pixels, observedOffset);
                referenceSum += CalculateLuminance(referencePixels, referenceOffset);
                sampleCount++;
            }
        }

        if (sampleCount == 0)
        {
            return -1;
        }

        var observedAverage = observedSum / sampleCount;
        var referenceAverage = referenceSum / sampleCount;
        double covariance = 0;
        double observedVariance = 0;
        double referenceVariance = 0;
        for (var y = 0; y < MinimapChampionReference.ImageSide; y++)
        {
            for (var x = 0; x < MinimapChampionReference.ImageSide; x++)
            {
                var deltaX = x - center;
                var deltaY = y - center;
                if ((deltaX * deltaX) + (deltaY * deltaY) > ComparisonRadiusSquared)
                {
                    continue;
                }

                var observedOffset = GetObservedOffset(
                    stride,
                    startX,
                    startY,
                    sourceSide,
                    x,
                    y);
                var referenceOffset = checked(
                    ((y * MinimapChampionReference.ImageSide) + x) * BytesPerPixel);
                var observedDelta = CalculateLuminance(pixels, observedOffset) - observedAverage;
                var referenceDelta = CalculateLuminance(referencePixels, referenceOffset) - referenceAverage;
                covariance += observedDelta * referenceDelta;
                observedVariance += observedDelta * observedDelta;
                referenceVariance += referenceDelta * referenceDelta;
            }
        }

        var denominator = Math.Sqrt(observedVariance * referenceVariance);
        return denominator <= double.Epsilon
            ? -1
            : Math.Clamp(covariance / denominator, -1, 1);
    }

    private static int GetObservedOffset(
        int stride,
        int startX,
        int startY,
        int sourceSide,
        int referenceX,
        int referenceY)
    {
        var sourceX = (int)Math.Round(
            referenceX * (sourceSide - 1d) /
            (MinimapChampionReference.ImageSide - 1d));
        var sourceY = (int)Math.Round(
            referenceY * (sourceSide - 1d) /
            (MinimapChampionReference.ImageSide - 1d));
        return checked(
            ((startY + sourceY) * stride) +
            ((startX + sourceX) * BytesPerPixel));
    }

    private static int CalculateLuminance(ReadOnlySpan<byte> pixels, int offset) =>
        ((29 * pixels[offset]) +
         (150 * pixels[offset + 1]) +
         (77 * pixels[offset + 2])) >> 8;

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
}
