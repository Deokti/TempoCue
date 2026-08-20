using System.Buffers;

namespace TempoCue.Vision;

/// <summary>
/// Находит компактные цветные контуры команд. Результат предназначен только для
/// настройки будущего распознавателя и ещё не отличает чемпионов от других значков.
/// </summary>
internal static class MinimapMarkerCandidateDetector
{
    private const int BytesPerPixel = 4;
    private const byte Ally = 1;
    private const byte Enemy = 2;
    private const byte VisitedOffset = 2;
    private const int MinimumSide = 5;
    private const int MaximumSide = 32;
    private const int MinimumColoredPixels = 12;
    private const double MinimumAspectRatio = 0.55;
    private const double MinimumDensity = 0.12;
    private const double MaximumDensity = 0.72;
    private const double MinimumConfidence = 0.35;

    public static MinimapMarkerCandidate[] Detect(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride)
    {
        ValidateImage(pixels, width, height, stride);
        var pixelCount = checked(width * height);
        var classifications = ArrayPool<byte>.Shared.Rent(pixelCount);
        var queue = ArrayPool<int>.Shared.Rent(pixelCount);
        try
        {
            ClassifyPixels(pixels, width, height, stride, classifications);
            var candidates = new List<MinimapMarkerCandidate>();
            for (var index = 0; index < pixelCount; index++)
            {
                var affiliation = classifications[index];
                if (affiliation is not Ally and not Enemy)
                {
                    continue;
                }

                var component = ReadComponent(
                    classifications,
                    queue,
                    index,
                    affiliation,
                    width,
                    height);
                var candidate = CreateCandidate(component, affiliation);
                if (candidate is not null)
                {
                    candidates.Add(candidate);
                }
            }

            return candidates
                .OrderBy(candidate => candidate.Y)
                .ThenBy(candidate => candidate.X)
                .ToArray();
        }
        finally
        {
            ArrayPool<int>.Shared.Return(queue);
            ArrayPool<byte>.Shared.Return(classifications);
        }
    }

    private static void ClassifyPixels(
        ReadOnlySpan<byte> pixels,
        int width,
        int height,
        int stride,
        Span<byte> classifications)
    {
        for (var y = 0; y < height; y++)
        {
            var rowOffset = checked(y * stride);
            var classificationOffset = checked(y * width);
            for (var x = 0; x < width; x++)
            {
                var pixelOffset = checked(rowOffset + (x * BytesPerPixel));
                classifications[classificationOffset + x] = Classify(
                    pixels[pixelOffset],
                    pixels[pixelOffset + 1],
                    pixels[pixelOffset + 2]);
            }
        }
    }

    private static byte Classify(byte blue, byte green, byte red)
    {
        if (red >= 145 && red - green >= 45 && red - blue >= 25)
        {
            return Enemy;
        }

        return blue >= 135 &&
               green >= 85 &&
               blue - red >= 35 &&
               green - red >= 15
            ? Ally
            : (byte)0;
    }

    private static Component ReadComponent(
        Span<byte> classifications,
        Span<int> queue,
        int startIndex,
        byte affiliation,
        int width,
        int height)
    {
        var head = 0;
        var tail = 1;
        queue[0] = startIndex;
        classifications[startIndex] = checked((byte)(affiliation + VisitedOffset));
        var startX = startIndex % width;
        var startY = startIndex / width;
        var minimumX = startX;
        var maximumX = startX;
        var minimumY = startY;
        var maximumY = startY;
        var coloredPixels = 0;

        while (head < tail)
        {
            var index = queue[head++];
            var x = index % width;
            var y = index / width;
            coloredPixels++;
            minimumX = Math.Min(minimumX, x);
            maximumX = Math.Max(maximumX, x);
            minimumY = Math.Min(minimumY, y);
            maximumY = Math.Max(maximumY, y);

            var startNeighborY = Math.Max(0, y - 1);
            var endNeighborY = Math.Min(height - 1, y + 1);
            var startNeighborX = Math.Max(0, x - 1);
            var endNeighborX = Math.Min(width - 1, x + 1);
            for (var neighborY = startNeighborY; neighborY <= endNeighborY; neighborY++)
            {
                for (var neighborX = startNeighborX; neighborX <= endNeighborX; neighborX++)
                {
                    var neighborIndex = checked((neighborY * width) + neighborX);
                    if (classifications[neighborIndex] != affiliation)
                    {
                        continue;
                    }

                    classifications[neighborIndex] = checked((byte)(affiliation + VisitedOffset));
                    queue[tail++] = neighborIndex;
                }
            }
        }

        return new Component(
            minimumX,
            minimumY,
            maximumX - minimumX + 1,
            maximumY - minimumY + 1,
            coloredPixels);
    }

    private static MinimapMarkerCandidate? CreateCandidate(
        Component component,
        byte affiliation)
    {
        var minimumSide = Math.Min(component.Width, component.Height);
        var maximumSide = Math.Max(component.Width, component.Height);
        if (minimumSide < MinimumSide ||
            maximumSide > MaximumSide ||
            component.ColoredPixels < MinimumColoredPixels)
        {
            return null;
        }

        var aspectRatio = minimumSide / (double)maximumSide;
        var density = component.ColoredPixels / (double)(component.Width * component.Height);
        if (aspectRatio < MinimumAspectRatio ||
            density is < MinimumDensity or > MaximumDensity)
        {
            return null;
        }

        var sizeScore = Math.Clamp(1 - (Math.Abs(maximumSide - 18) / 18d), 0, 1);
        var densityScore = Math.Clamp(1 - (Math.Abs(density - 0.32) / 0.32), 0, 1);
        var confidence = (sizeScore * 0.4) + (aspectRatio * 0.35) + (densityScore * 0.25);
        if (confidence < MinimumConfidence)
        {
            return null;
        }

        return new MinimapMarkerCandidate(
            component.X,
            component.Y,
            component.Width,
            component.Height,
            affiliation == Ally
                ? MinimapMarkerAffiliation.Ally
                : MinimapMarkerAffiliation.Enemy,
            confidence);
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

    private readonly record struct Component(
        int X,
        int Y,
        int Width,
        int Height,
        int ColoredPixels);
}
