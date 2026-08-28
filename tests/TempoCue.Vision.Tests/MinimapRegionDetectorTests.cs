namespace TempoCue.Vision.Tests;

public sealed class MinimapRegionDetectorTests
{
    private const int CandidateSide = 720;
    private const int Stride = CandidateSide * 4;
    private const int FrameWidth = 3440;
    private const int FrameHeight = 1440;

    [Fact]
    public void Detect_FindsBottomRightMinimapOfConfiguredScale()
    {
        var left = CreateBackground();
        var right = CreateBackground();
        DrawMinimap(right, side: 439, MinimapScreenCorner.BottomRight);

        var detection = MinimapRegionDetector.Detect(
            left.Pixels,
            right.Pixels,
            CandidateSide,
            Stride,
            FrameWidth,
            FrameHeight);

        Assert.NotNull(detection);
        Assert.Equal(MinimapScreenCorner.BottomRight, detection.Corner);
        Assert.InRange(detection.Region.Width, 437, 441);
        Assert.Equal(FrameWidth - detection.Region.Width, detection.Region.X);
        Assert.Equal(FrameHeight - detection.Region.Height, detection.Region.Y);
        Assert.True(detection.Confidence > 0.5);
    }

    [Fact]
    public void Detect_FindsFlippedBottomLeftMinimap()
    {
        var left = CreateBackground();
        var right = CreateBackground();
        DrawMinimap(left, side: 330, MinimapScreenCorner.BottomLeft);

        var detection = MinimapRegionDetector.Detect(
            left.Pixels,
            right.Pixels,
            CandidateSide,
            Stride,
            FrameWidth,
            FrameHeight);

        Assert.NotNull(detection);
        Assert.Equal(MinimapScreenCorner.BottomLeft, detection.Corner);
        Assert.InRange(detection.Region.Width, 328, 332);
        Assert.Equal(0, detection.Region.X);
        Assert.Equal(FrameHeight - detection.Region.Height, detection.Region.Y);
    }

    [Fact]
    public void Detect_ReturnsNullWithoutTwoSquareBoundaries()
    {
        var left = CreateBackground();
        var right = CreateBackground();

        var detection = MinimapRegionDetector.Detect(
            left.Pixels,
            right.Pixels,
            CandidateSide,
            Stride,
            FrameWidth,
            FrameHeight);

        Assert.Null(detection);
    }

    private static BgraTestImage CreateBackground() =>
        new(CandidateSide, CandidateSide, 54, 67, 58);

    private static void DrawMinimap(
        BgraTestImage image,
        int side,
        MinimapScreenCorner corner)
    {
        var startX = corner == MinimapScreenCorner.BottomRight
            ? CandidateSide - side
            : 0;
        var startY = CandidateSide - side;
        for (var y = startY; y < CandidateSide; y++)
        {
            for (var x = startX; x < startX + side; x++)
            {
                var border =
                    y - startY < 5 ||
                    (corner == MinimapScreenCorner.BottomRight
                        ? x - startX < 5
                        : startX + side - x <= 5);
                if (border)
                {
                    image.SetPixel(x, y, 207, 171, 79);
                    continue;
                }

                var pattern = ((x / 17) + (y / 13)) % 2 == 0;
                image.SetPixel(
                    x,
                    y,
                    pattern ? (byte)18 : (byte)105,
                    pattern ? (byte)30 : (byte)118,
                    pattern ? (byte)25 : (byte)92);
            }
        }
    }

}
