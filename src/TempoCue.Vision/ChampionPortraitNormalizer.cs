namespace TempoCue.Vision;

/// <summary>
/// Приводит квадратную центральную часть портрета к геометрии эталона миникарты.
/// </summary>
public static class ChampionPortraitNormalizer
{
    public static byte[] NormalizeBgra32(
        ReadOnlySpan<byte> sourcePixels,
        int sourceWidth,
        int sourceHeight,
        int sourceStride)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        var minimumStride = checked(sourceWidth * MinimapChampionReference.BytesPerPixel);
        if (sourceStride < minimumStride)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sourceStride),
                "Шаг строки BGRA32 меньше ширины изображения.");
        }

        var requiredLength = checked(sourceStride * sourceHeight);
        if (sourcePixels.Length < requiredLength)
        {
            throw new ArgumentException(
                "Массив BGRA32 не содержит все строки изображения.",
                nameof(sourcePixels));
        }

        var targetSide = MinimapChampionReference.ImageSide;
        var targetStride = targetSide * MinimapChampionReference.BytesPerPixel;
        var targetPixels = new byte[targetStride * targetSide];
        var squareSide = Math.Min(sourceWidth, sourceHeight);
        var squareX = (sourceWidth - squareSide) / 2;
        var squareY = (sourceHeight - squareSide) / 2;

        for (var targetY = 0; targetY < targetSide; targetY++)
        {
            var sourceY = MapCoordinate(targetY, targetSide, squareSide, squareY);
            var top = (int)Math.Floor(sourceY);
            var bottom = Math.Min(top + 1, squareY + squareSide - 1);
            var verticalWeight = sourceY - top;

            for (var targetX = 0; targetX < targetSide; targetX++)
            {
                var sourceX = MapCoordinate(targetX, targetSide, squareSide, squareX);
                var left = (int)Math.Floor(sourceX);
                var right = Math.Min(left + 1, squareX + squareSide - 1);
                var horizontalWeight = sourceX - left;
                var targetOffset = (targetY * targetStride) +
                                   (targetX * MinimapChampionReference.BytesPerPixel);

                for (var channel = 0;
                     channel < MinimapChampionReference.BytesPerPixel;
                     channel++)
                {
                    var topLeft = sourcePixels[(top * sourceStride) + (left * 4) + channel];
                    var topRight = sourcePixels[(top * sourceStride) + (right * 4) + channel];
                    var bottomLeft = sourcePixels[(bottom * sourceStride) + (left * 4) + channel];
                    var bottomRight = sourcePixels[(bottom * sourceStride) + (right * 4) + channel];
                    var topValue = Interpolate(topLeft, topRight, horizontalWeight);
                    var bottomValue = Interpolate(bottomLeft, bottomRight, horizontalWeight);
                    targetPixels[targetOffset + channel] = (byte)Math.Clamp(
                        Math.Round(Interpolate(topValue, bottomValue, verticalWeight)),
                        byte.MinValue,
                        byte.MaxValue);
                }
            }
        }

        return targetPixels;
    }

    private static double MapCoordinate(
        int targetCoordinate,
        int targetSide,
        int sourceSide,
        int sourceOffset)
    {
        var mapped = sourceOffset +
                     (((targetCoordinate + 0.5d) * sourceSide / targetSide) - 0.5d);
        return Math.Clamp(mapped, sourceOffset, sourceOffset + sourceSide - 1d);
    }

    private static double Interpolate(double first, double second, double weight) =>
        first + ((second - first) * weight);
}
