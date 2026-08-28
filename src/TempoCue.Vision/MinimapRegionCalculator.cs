namespace TempoCue.Vision;

/// <summary>Вычисляет поисковую область миникарты без доступа к пикселям.</summary>
internal static class MinimapRegionCalculator
{
    private const double SearchSizeToFrameHeight = 0.50;

    public static PixelRegion CalculateSearchRegion(
        int frameWidth,
        int frameHeight,
        MinimapScreenCorner corner)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameHeight);

        var side = Math.Clamp(
            (int)Math.Round(
                frameHeight * SearchSizeToFrameHeight,
                MidpointRounding.AwayFromZero),
            1,
            Math.Min(frameWidth, frameHeight));
        var x = corner == MinimapScreenCorner.BottomRight
            ? frameWidth - side
            : 0;

        return new PixelRegion(
            x,
            frameHeight - side,
            side,
            side);
    }
}
