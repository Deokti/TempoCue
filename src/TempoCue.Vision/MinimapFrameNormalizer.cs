namespace TempoCue.Vision;

/// <summary>Приводит изображение миникарты BGRA32 к фиксированной геометрии.</summary>
public static class MinimapFrameNormalizer
{
    public const int CanonicalSize = 320;
    private const int BytesPerPixel = 4;

    public static byte[] Normalize(
        ReadOnlySpan<byte> sourcePixels,
        int sourceWidth,
        int sourceHeight,
        int sourceStride)
    {
        var destinationStride = checked(CanonicalSize * BytesPerPixel);
        var destination = GC.AllocateUninitializedArray<byte>(
            checked(destinationStride * CanonicalSize));
        Normalize(
            sourcePixels,
            sourceWidth,
            sourceHeight,
            sourceStride,
            destination,
            CanonicalSize,
            CanonicalSize,
            destinationStride);
        return destination;
    }

    /// <summary>
    /// Записывает нормализованное изображение в переданный буфер. Будущий цикл анализа сможет
    /// переиспользовать этот буфер и не создавать большой массив на каждом кадре.
    /// </summary>
    public static void Normalize(
        ReadOnlySpan<byte> sourcePixels,
        int sourceWidth,
        int sourceHeight,
        int sourceStride,
        Span<byte> destinationPixels,
        int destinationWidth,
        int destinationHeight,
        int destinationStride)
    {
        ValidateImage(sourcePixels.Length, sourceWidth, sourceHeight, sourceStride, "sourcePixels");
        ValidateImage(
            destinationPixels.Length,
            destinationWidth,
            destinationHeight,
            destinationStride,
            "destinationPixels");

        if (sourceWidth == destinationWidth && sourceHeight == destinationHeight)
        {
            CopyRows(
                sourcePixels,
                sourceWidth,
                sourceHeight,
                sourceStride,
                destinationPixels,
                destinationStride);
            return;
        }

        for (var destinationY = 0; destinationY < destinationHeight; destinationY++)
        {
            var sourceY = GetSourceCoordinate(destinationY, destinationHeight, sourceHeight);
            var sourceY0 = (int)sourceY;
            var sourceY1 = Math.Min(sourceY0 + 1, sourceHeight - 1);
            var verticalWeight = sourceY - sourceY0;

            for (var destinationX = 0; destinationX < destinationWidth; destinationX++)
            {
                var sourceX = GetSourceCoordinate(destinationX, destinationWidth, sourceWidth);
                var sourceX0 = (int)sourceX;
                var sourceX1 = Math.Min(sourceX0 + 1, sourceWidth - 1);
                var horizontalWeight = sourceX - sourceX0;
                var destinationOffset = checked(
                    (destinationY * destinationStride) + (destinationX * BytesPerPixel));

                for (var channel = 0; channel < BytesPerPixel; channel++)
                {
                    var topLeft = sourcePixels[checked(
                        (sourceY0 * sourceStride) + (sourceX0 * BytesPerPixel) + channel)];
                    var topRight = sourcePixels[checked(
                        (sourceY0 * sourceStride) + (sourceX1 * BytesPerPixel) + channel)];
                    var bottomLeft = sourcePixels[checked(
                        (sourceY1 * sourceStride) + (sourceX0 * BytesPerPixel) + channel)];
                    var bottomRight = sourcePixels[checked(
                        (sourceY1 * sourceStride) + (sourceX1 * BytesPerPixel) + channel)];
                    var top = Lerp(topLeft, topRight, horizontalWeight);
                    var bottom = Lerp(bottomLeft, bottomRight, horizontalWeight);
                    destinationPixels[destinationOffset + channel] = (byte)Math.Round(
                        Lerp(top, bottom, verticalWeight),
                        MidpointRounding.AwayFromZero);
                }
            }
        }
    }

    private static double GetSourceCoordinate(
        int destinationCoordinate,
        int destinationLength,
        int sourceLength)
    {
        var coordinate = ((destinationCoordinate + 0.5) * sourceLength / destinationLength) - 0.5;
        return Math.Clamp(coordinate, 0, sourceLength - 1);
    }

    private static double Lerp(double start, double end, double weight) =>
        start + ((end - start) * weight);

    private static void CopyRows(
        ReadOnlySpan<byte> sourcePixels,
        int width,
        int height,
        int sourceStride,
        Span<byte> destinationPixels,
        int destinationStride)
    {
        var rowLength = checked(width * BytesPerPixel);
        for (var row = 0; row < height; row++)
        {
            sourcePixels.Slice(checked(row * sourceStride), rowLength).CopyTo(
                destinationPixels.Slice(checked(row * destinationStride), rowLength));
        }
    }

    private static void ValidateImage(
        int bufferLength,
        int width,
        int height,
        int stride,
        string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        var minimumStride = checked(width * BytesPerPixel);
        if (stride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stride),
                stride,
                "Шаг строки BGRA32 не может быть меньше ширины изображения в байтах.");
        }

        var requiredLength = checked(((height - 1) * stride) + minimumStride);
        if (bufferLength < requiredLength)
        {
            throw new ArgumentException(
                "Буфер не содержит все строки изображения BGRA32.",
                parameterName);
        }
    }
}
