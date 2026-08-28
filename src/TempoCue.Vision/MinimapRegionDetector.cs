namespace TempoCue.Vision;

/// <summary>
/// Ищет квадратную границу миникарты внутри одного из нижних углов кадра.
/// Алгоритм использует длинные верхнюю и боковую границы, поэтому не зависит
/// от языка, цвета команд и динамических значков внутри карты.
/// </summary>
internal static class MinimapRegionDetector
{
    private const int BytesPerPixel = 4;
    private const double MinimumMapSizeToFrameHeight = 0.08;
    private const double MinimumBoundaryScore = 13;
    private const int BoundarySampleStep = 3;
    private const int BoundaryComparisonDistance = 3;
    private const int CompetingPeakDistance = 10;

    public static MinimapRegionDetection? Detect(
        ReadOnlySpan<byte> leftCandidate,
        ReadOnlySpan<byte> rightCandidate,
        int candidateSide,
        int stride,
        int frameWidth,
        int frameHeight)
    {
        ValidateImage(leftCandidate, candidateSide, stride);
        ValidateImage(rightCandidate, candidateSide, stride);

        var minimumSide = Math.Clamp(
            (int)Math.Round(
                frameHeight * MinimumMapSizeToFrameHeight,
                MidpointRounding.AwayFromZero),
            BoundaryComparisonDistance * 4,
            candidateSide - BoundaryComparisonDistance - 1);
        var left = DetectCorner(
            leftCandidate,
            candidateSide,
            stride,
            frameWidth,
            frameHeight,
            minimumSide,
            MinimapScreenCorner.BottomLeft);
        var right = DetectCorner(
            rightCandidate,
            candidateSide,
            stride,
            frameWidth,
            frameHeight,
            minimumSide,
            MinimapScreenCorner.BottomRight);

        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return right.BoundaryScore >= left.BoundaryScore ? right : left;
    }

    private static MinimapRegionDetection? DetectCorner(
        ReadOnlySpan<byte> pixels,
        int candidateSide,
        int stride,
        int frameWidth,
        int frameHeight,
        int minimumSide,
        MinimapScreenCorner corner)
    {
        var best = default(BoundaryCandidate);
        var secondBest = default(BoundaryCandidate);

        for (var side = minimumSide;
             side <= candidateSide - BoundaryComparisonDistance - 1;
             side++)
        {
            var top = candidateSide - side;
            var innerBoundary = corner == MinimapScreenCorner.BottomRight
                ? candidateSide - side
                : side - 1;
            if (top < BoundaryComparisonDistance ||
                innerBoundary < BoundaryComparisonDistance ||
                innerBoundary >= candidateSide - BoundaryComparisonDistance)
            {
                continue;
            }

            var horizontalStart = corner == MinimapScreenCorner.BottomRight
                ? candidateSide - side
                : 0;
            var horizontalEnd = horizontalStart + side;
            var topScore = CalculateHorizontalBoundaryScore(
                pixels,
                stride,
                top,
                horizontalStart,
                horizontalEnd);
            var verticalScore = CalculateVerticalBoundaryScore(
                pixels,
                stride,
                innerBoundary,
                top,
                candidateSide);
            var score = BalancedScore(topScore, verticalScore);
            var candidate = new BoundaryCandidate(side, score, topScore, verticalScore);

            if (candidate.Score > best.Score)
            {
                if (Math.Abs(candidate.Side - best.Side) >= CompetingPeakDistance)
                {
                    secondBest = best;
                }

                best = candidate;
            }
            else if (
                candidate.Score > secondBest.Score &&
                Math.Abs(candidate.Side - best.Side) >= CompetingPeakDistance)
            {
                secondBest = candidate;
            }
        }

        if (best.Score < MinimumBoundaryScore ||
            best.TopScore < MinimumBoundaryScore * 0.65 ||
            best.SideScore < MinimumBoundaryScore * 0.65)
        {
            return null;
        }

        var absoluteConfidence = Math.Clamp(
            (best.Score - MinimumBoundaryScore) / 24,
            0,
            1);
        var separationConfidence = Math.Clamp(
            (best.Score - secondBest.Score) / 10,
            0,
            1);
        var confidence = (absoluteConfidence * 0.75) + (separationConfidence * 0.25);
        var globalX = corner == MinimapScreenCorner.BottomRight
            ? frameWidth - best.Side
            : 0;

        return new MinimapRegionDetection(
            new PixelRegion(
                globalX,
                frameHeight - best.Side,
                best.Side,
                best.Side),
            corner,
            confidence,
            best.Score);
    }

    private static double CalculateHorizontalBoundaryScore(
        ReadOnlySpan<byte> pixels,
        int stride,
        int boundaryY,
        int startX,
        int endX)
    {
        var margin = Math.Max(4, (endX - startX) / 12);
        var sum = 0d;
        var samples = 0;
        for (var x = startX + margin; x < endX - margin; x += BoundarySampleStep)
        {
            sum += ColorDistance(
                pixels,
                stride,
                x,
                boundaryY - BoundaryComparisonDistance,
                x,
                boundaryY + BoundaryComparisonDistance);
            samples++;
        }

        return samples == 0 ? 0 : sum / samples;
    }

    private static double CalculateVerticalBoundaryScore(
        ReadOnlySpan<byte> pixels,
        int stride,
        int boundaryX,
        int startY,
        int endY)
    {
        var margin = Math.Max(4, (endY - startY) / 12);
        var sum = 0d;
        var samples = 0;
        for (var y = startY + margin; y < endY - margin; y += BoundarySampleStep)
        {
            sum += ColorDistance(
                pixels,
                stride,
                boundaryX - BoundaryComparisonDistance,
                y,
                boundaryX + BoundaryComparisonDistance,
                y);
            samples++;
        }

        return samples == 0 ? 0 : sum / samples;
    }

    private static double ColorDistance(
        ReadOnlySpan<byte> pixels,
        int stride,
        int firstX,
        int firstY,
        int secondX,
        int secondY)
    {
        var first = checked((firstY * stride) + (firstX * BytesPerPixel));
        var second = checked((secondY * stride) + (secondX * BytesPerPixel));
        return (
            Math.Abs(pixels[first] - pixels[second]) +
            Math.Abs(pixels[first + 1] - pixels[second + 1]) +
            Math.Abs(pixels[first + 2] - pixels[second + 2])) / 3d;
    }

    private static double BalancedScore(double first, double second)
    {
        if (first <= 0 || second <= 0)
        {
            return 0;
        }

        // Гармоническое среднее не позволяет одной случайной линии скрыть
        // отсутствие второй стороны квадратной границы.
        return 2 * first * second / (first + second);
    }

    private static void ValidateImage(
        ReadOnlySpan<byte> pixels,
        int side,
        int stride)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(side);
        if (stride < checked(side * BytesPerPixel))
        {
            throw new ArgumentOutOfRangeException(nameof(stride));
        }

        if (pixels.Length < checked(stride * side))
        {
            throw new ArgumentException("Размер массива не соответствует изображению.", nameof(pixels));
        }
    }

    private readonly record struct BoundaryCandidate(
        int Side,
        double Score,
        double TopScore,
        double SideScore);
}
