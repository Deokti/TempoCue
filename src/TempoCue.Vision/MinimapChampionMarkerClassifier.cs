namespace TempoCue.Vision;

/// <summary>
/// Отделяет портретоподобные значки от башен, крипов и других цветных символов.
/// Эвристика намеренно используется только для диагностики до проверки на реальных кадрах.
/// </summary>
internal static class MinimapChampionMarkerClassifier
{
    private const int BytesPerPixel = 4;
    private const int MarkerSide = 25;
    private const int MarkerRadius = MarkerSide / 2;
    private const int SearchRadius = 10;
    private const int SearchStep = 2;
    private const int InteriorInset = 6;
    private const int DuplicateCenterDistance = 10;
    private const double MinimumChampionConfidence = 0.38;
    private const int RadialSampleCount = 32;

    public static MinimapChampionMarkerCandidate[] Classify(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride,
        IReadOnlyList<MinimapMarkerCandidate> candidates)
    {
        ValidateImage(pixels, width, height, stride);
        ArgumentNullException.ThrowIfNull(candidates);

        var matches = new List<MinimapChampionMarkerCandidate>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var classified = FindBestMatch(pixels, width, height, stride, candidate);
            if (classified is not null)
            {
                matches.Add(classified);
            }
        }

        return RemoveDuplicates(matches);
    }

    private static MinimapChampionMarkerCandidate? FindBestMatch(
        ReadOnlySpan<byte> pixels,
        int imageWidth,
        int imageHeight,
        int stride,
        MinimapMarkerCandidate seed)
    {
        if (!IsInsideImage(seed, imageWidth, imageHeight))
        {
            return null;
        }

        var seedCenterX = seed.X + ((seed.Width - 1) / 2d);
        var seedCenterY = seed.Y + ((seed.Height - 1) / 2d);
        MinimapChampionMarkerCandidate? bestMatch = null;
        for (var offsetY = -SearchRadius; offsetY <= SearchRadius; offsetY += SearchStep)
        {
            for (var offsetX = -SearchRadius; offsetX <= SearchRadius; offsetX += SearchStep)
            {
                var centerX = (int)Math.Round(seedCenterX + offsetX);
                var centerY = (int)Math.Round(seedCenterY + offsetY);
                var patchX = centerX - MarkerRadius;
                var patchY = centerY - MarkerRadius;
                if (patchX < 0 ||
                    patchY < 0 ||
                    patchX > imageWidth - MarkerSide ||
                    patchY > imageHeight - MarkerSide)
                {
                    continue;
                }

                var confidence = EvaluatePatch(
                    pixels,
                    stride,
                    patchX,
                    patchY,
                    seed.Affiliation,
                    offsetX,
                    offsetY);
                if (confidence < MinimumChampionConfidence ||
                    bestMatch is not null && bestMatch.Confidence >= confidence)
                {
                    continue;
                }

                bestMatch = new MinimapChampionMarkerCandidate(
                    patchX,
                    patchY,
                    MarkerSide,
                    MarkerSide,
                    seed.Affiliation,
                    confidence);
            }
        }

        return bestMatch;
    }

    private static double EvaluatePatch(
        ReadOnlySpan<byte> pixels,
        int stride,
        int patchX,
        int patchY,
        MinimapMarkerAffiliation affiliation,
        int offsetX,
        int offsetY)
    {
        var ringScore = CalculateRingScore(
            pixels,
            stride,
            patchX,
            patchY,
            affiliation);
        var teamRingCoverage = MeasureTeamRingAngularCoverage(
            pixels,
            stride,
            patchX,
            patchY,
            affiliation);
        var texture = MeasureInterior(
            pixels,
            stride,
            patchX + InteriorInset,
            patchY + InteriorInset,
            MarkerSide - (InteriorInset * 2),
            MarkerSide - (InteriorInset * 2));
        var textureScore = Math.Clamp((texture.StandardDeviation - 8) / 34, 0, 1);
        var edgeScore = Math.Clamp((texture.AverageNeighborDifference - 4) / 28, 0, 1);
        var chromaScore = Math.Clamp((texture.AverageChroma - 10) / 50, 0, 1);
        var radialEdgeCoverage = MeasureRadialEdgeCoverage(
            pixels,
            stride,
            patchX,
            patchY);
        var foregroundCoverage = MeasureForegroundCoverage(
            pixels,
            stride,
            patchX,
            patchY);
        var radialEdgeScore = Math.Clamp((radialEdgeCoverage - 0.15) / 0.55, 0, 1);
        var foregroundScore = Math.Clamp((foregroundCoverage - 0.15) / 0.60, 0, 1);
        var teamRingCoverageScore = Math.Clamp((teamRingCoverage - 0.25) / 0.60, 0, 1);
        var offsetDistance = Math.Sqrt((offsetX * offsetX) + (offsetY * offsetY));
        var proximityScore = Math.Clamp(
            1 - (offsetDistance / (SearchRadius * Math.Sqrt(2))),
            0,
            1);
        var confidence =
            (ringScore * 0.10) +
            (teamRingCoverageScore * 0.20) +
            (textureScore * 0.15) +
            (edgeScore * 0.10) +
            (chromaScore * 0.05) +
            (radialEdgeScore * 0.25) +
            (foregroundScore * 0.15) +
            (proximityScore * 0.05);

        return ringScore < 0.05 ||
               teamRingCoverage < 0.30 ||
               texture.LuminanceRange < 28 ||
               texture.StandardDeviation < 8 ||
               radialEdgeCoverage < 0.20 ||
               foregroundCoverage < 0.20
            ? 0
            : confidence;
    }

    private static double MeasureTeamRingAngularCoverage(
        ReadOnlySpan<byte> pixels,
        int stride,
        int patchX,
        int patchY,
        MinimapMarkerAffiliation affiliation)
    {
        var centerX = patchX + MarkerRadius;
        var centerY = patchY + MarkerRadius;
        var coveredDirections = 0;
        for (var sample = 0; sample < RadialSampleCount; sample++)
        {
            var angle = sample * (Math.PI * 2 / RadialSampleCount);
            var directionX = Math.Cos(angle);
            var directionY = Math.Sin(angle);
            var directionCovered = false;
            for (var radius = 8; radius <= 12; radius++)
            {
                var x = (int)Math.Round(centerX + (directionX * radius));
                var y = (int)Math.Round(centerY + (directionY * radius));
                var offset = checked((y * stride) + (x * BytesPerPixel));
                if (!IsTeamPixel(
                        pixels[offset],
                        pixels[offset + 1],
                        pixels[offset + 2],
                        affiliation))
                {
                    continue;
                }

                directionCovered = true;
                break;
            }

            coveredDirections += directionCovered ? 1 : 0;
        }

        return coveredDirections / (double)RadialSampleCount;
    }

    private static double MeasureRadialEdgeCoverage(
        ReadOnlySpan<byte> pixels,
        int stride,
        int patchX,
        int patchY)
    {
        var centerX = patchX + MarkerRadius;
        var centerY = patchY + MarkerRadius;
        var strongEdges = 0;
        for (var sample = 0; sample < RadialSampleCount; sample++)
        {
            var angle = sample * (Math.PI * 2 / RadialSampleCount);
            var directionX = Math.Cos(angle);
            var directionY = Math.Sin(angle);
            var innerX = (int)Math.Round(centerX + (directionX * 7));
            var innerY = (int)Math.Round(centerY + (directionY * 7));
            var ringX = (int)Math.Round(centerX + (directionX * 11));
            var ringY = (int)Math.Round(centerY + (directionY * 11));
            var innerOffset = checked((innerY * stride) + (innerX * BytesPerPixel));
            var ringOffset = checked((ringY * stride) + (ringX * BytesPerPixel));
            if (CalculateColorDistanceSquared(pixels, innerOffset, ringOffset) >= 900)
            {
                strongEdges++;
            }
        }

        return strongEdges / (double)RadialSampleCount;
    }

    private static double MeasureForegroundCoverage(
        ReadOnlySpan<byte> pixels,
        int stride,
        int patchX,
        int patchY)
    {
        long backgroundBlue = 0;
        long backgroundGreen = 0;
        long backgroundRed = 0;
        var backgroundCount = 0;
        const double center = MarkerRadius;

        for (var y = 0; y < MarkerSide; y++)
        {
            for (var x = 0; x < MarkerSide; x++)
            {
                var normalizedX = (x - center) / MarkerRadius;
                var normalizedY = (y - center) / MarkerRadius;
                var normalizedRadiusSquared =
                    (normalizedX * normalizedX) + (normalizedY * normalizedY);
                if (normalizedRadiusSquared < 1.21)
                {
                    continue;
                }

                var offset = checked(
                    ((patchY + y) * stride) + ((patchX + x) * BytesPerPixel));
                backgroundBlue += pixels[offset];
                backgroundGreen += pixels[offset + 1];
                backgroundRed += pixels[offset + 2];
                backgroundCount++;
            }
        }

        if (backgroundCount == 0)
        {
            return 0;
        }

        var averageBlue = backgroundBlue / (double)backgroundCount;
        var averageGreen = backgroundGreen / (double)backgroundCount;
        var averageRed = backgroundRed / (double)backgroundCount;
        var foregroundCount = 0;
        var centerCount = 0;
        for (var y = 0; y < MarkerSide; y++)
        {
            for (var x = 0; x < MarkerSide; x++)
            {
                var normalizedX = (x - center) / MarkerRadius;
                var normalizedY = (y - center) / MarkerRadius;
                if ((normalizedX * normalizedX) + (normalizedY * normalizedY) > 0.34)
                {
                    continue;
                }

                var offset = checked(
                    ((patchY + y) * stride) + ((patchX + x) * BytesPerPixel));
                var blueDifference = pixels[offset] - averageBlue;
                var greenDifference = pixels[offset + 1] - averageGreen;
                var redDifference = pixels[offset + 2] - averageRed;
                var distanceSquared =
                    (blueDifference * blueDifference) +
                    (greenDifference * greenDifference) +
                    (redDifference * redDifference);
                centerCount++;
                foregroundCount += distanceSquared >= 900 ? 1 : 0;
            }
        }

        return centerCount == 0 ? 0 : foregroundCount / (double)centerCount;
    }

    private static int CalculateColorDistanceSquared(
        ReadOnlySpan<byte> pixels,
        int firstOffset,
        int secondOffset)
    {
        var blueDifference = pixels[firstOffset] - pixels[secondOffset];
        var greenDifference = pixels[firstOffset + 1] - pixels[secondOffset + 1];
        var redDifference = pixels[firstOffset + 2] - pixels[secondOffset + 2];
        return (blueDifference * blueDifference) +
               (greenDifference * greenDifference) +
               (redDifference * redDifference);
    }

    private static MinimapChampionMarkerCandidate[] RemoveDuplicates(
        List<MinimapChampionMarkerCandidate> matches)
    {
        var result = new List<MinimapChampionMarkerCandidate>(matches.Count);
        var maximumDistanceSquared = DuplicateCenterDistance * DuplicateCenterDistance;
        foreach (var match in matches.OrderByDescending(candidate => candidate.Confidence))
        {
            var centerX = match.X + MarkerRadius;
            var centerY = match.Y + MarkerRadius;
            var overlapsExisting = result.Any(existing =>
            {
                var deltaX = centerX - (existing.X + MarkerRadius);
                var deltaY = centerY - (existing.Y + MarkerRadius);
                return (deltaX * deltaX) + (deltaY * deltaY) <= maximumDistanceSquared;
            });
            if (!overlapsExisting)
            {
                result.Add(match);
            }
        }

        return result
            .OrderBy(candidate => candidate.Y)
            .ThenBy(candidate => candidate.X)
            .ToArray();
    }

    private static double CalculateRingScore(
        ReadOnlySpan<byte> pixels,
        int stride,
        int patchX,
        int patchY,
        MinimapMarkerAffiliation affiliation)
    {
        var ringTeamPixels = 0;
        var ringPixels = 0;
        var interiorTeamPixels = 0;
        var interiorPixels = 0;
        const double center = MarkerRadius;

        for (var y = 0; y < MarkerSide; y++)
        {
            for (var x = 0; x < MarkerSide; x++)
            {
                var normalizedX = (x - center) / MarkerRadius;
                var normalizedY = (y - center) / MarkerRadius;
                var normalizedRadius = Math.Sqrt(
                    (normalizedX * normalizedX) + (normalizedY * normalizedY));
                var pixelOffset = checked(
                    ((patchY + y) * stride) + ((patchX + x) * BytesPerPixel));
                var isTeamPixel = IsTeamPixel(
                    pixels[pixelOffset],
                    pixels[pixelOffset + 1],
                    pixels[pixelOffset + 2],
                    affiliation);

                if (normalizedRadius is >= 0.65 and <= 1.10)
                {
                    ringPixels++;
                    ringTeamPixels += isTeamPixel ? 1 : 0;
                }
                else if (normalizedRadius < 0.58)
                {
                    interiorPixels++;
                    interiorTeamPixels += isTeamPixel ? 1 : 0;
                }
            }
        }

        var ringRatio = ringPixels == 0
            ? 0
            : ringTeamPixels / (double)ringPixels;
        var interiorRatio = interiorPixels == 0
            ? 0
            : interiorTeamPixels / (double)interiorPixels;
        // Портрет самого чемпиона может содержать цвет своей команды, поэтому
        // внутренние пиксели ослабляют, но не обнуляют свидетельство внешнего кольца.
        return Math.Clamp((ringRatio - (interiorRatio * 0.35)) / 0.35, 0, 1);
    }

    private static TextureMetrics MeasureInterior(
        ReadOnlySpan<byte> pixels,
        int stride,
        int startX,
        int startY,
        int width,
        int height)
    {
        var count = checked(width * height);
        long luminanceSum = 0;
        long luminanceSquaredSum = 0;
        long chromaSum = 0;
        long neighborDifferenceSum = 0;
        var neighborDifferenceCount = 0;
        var minimumLuminance = 255;
        var maximumLuminance = 0;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixelOffset = checked(
                    ((startY + y) * stride) + ((startX + x) * BytesPerPixel));
                var blue = pixels[pixelOffset];
                var green = pixels[pixelOffset + 1];
                var red = pixels[pixelOffset + 2];
                var luminance = CalculateLuminance(blue, green, red);
                luminanceSum += luminance;
                luminanceSquaredSum += luminance * luminance;
                chromaSum += Math.Max(red, Math.Max(green, blue)) -
                    Math.Min(red, Math.Min(green, blue));
                minimumLuminance = Math.Min(minimumLuminance, luminance);
                maximumLuminance = Math.Max(maximumLuminance, luminance);

                if (x > 0)
                {
                    neighborDifferenceSum += Math.Abs(
                        luminance - CalculateLuminanceAt(pixels, pixelOffset - BytesPerPixel));
                    neighborDifferenceCount++;
                }

                if (y > 0)
                {
                    neighborDifferenceSum += Math.Abs(
                        luminance - CalculateLuminanceAt(pixels, pixelOffset - stride));
                    neighborDifferenceCount++;
                }
            }
        }

        var averageLuminance = luminanceSum / (double)count;
        var variance = Math.Max(
            0,
            (luminanceSquaredSum / (double)count) -
            (averageLuminance * averageLuminance));
        return new TextureMetrics(
            Math.Sqrt(variance),
            maximumLuminance - minimumLuminance,
            neighborDifferenceCount == 0
                ? 0
                : neighborDifferenceSum / (double)neighborDifferenceCount,
            chromaSum / (double)count);
    }

    private static int CalculateLuminanceAt(ReadOnlySpan<byte> pixels, int offset) =>
        CalculateLuminance(pixels[offset], pixels[offset + 1], pixels[offset + 2]);

    private static int CalculateLuminance(byte blue, byte green, byte red) =>
        ((29 * blue) + (150 * green) + (77 * red)) >> 8;

    private static bool IsTeamPixel(
        byte blue,
        byte green,
        byte red,
        MinimapMarkerAffiliation affiliation) =>
        affiliation switch
        {
            MinimapMarkerAffiliation.Enemy =>
                red >= 145 && red - green >= 45 && red - blue >= 25,
            MinimapMarkerAffiliation.Ally =>
                blue >= 135 &&
                green >= 85 &&
                blue - red >= 35 &&
                green - red >= 15,
            _ => false
        };

    private static bool IsInsideImage(
        MinimapMarkerCandidate candidate,
        int width,
        int height) =>
        candidate.X >= 0 &&
        candidate.Y >= 0 &&
        candidate.Width > 0 &&
        candidate.Height > 0 &&
        candidate.X <= width - candidate.Width &&
        candidate.Y <= height - candidate.Height;

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

    private readonly record struct TextureMetrics(
        double StandardDeviation,
        int LuminanceRange,
        double AverageNeighborDifference,
        double AverageChroma);
}
