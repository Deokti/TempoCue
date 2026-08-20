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
            left,
            right,
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
            left,
            right,
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
            left,
            right,
            CandidateSide,
            Stride,
            FrameWidth,
            FrameHeight);

        Assert.Null(detection);
    }

    private static byte[] CreateBackground()
    {
        var pixels = new byte[Stride * CandidateSide];
        for (var y = 0; y < CandidateSide; y++)
        {
            for (var x = 0; x < CandidateSide; x++)
            {
                SetPixel(pixels, x, y, 54, 67, 58);
            }
        }

        return pixels;
    }

    private static void DrawMinimap(
        byte[] pixels,
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
                    SetPixel(pixels, x, y, 207, 171, 79);
                    continue;
                }

                var pattern = ((x / 17) + (y / 13)) % 2 == 0;
                SetPixel(
                    pixels,
                    x,
                    y,
                    pattern ? (byte)18 : (byte)105,
                    pattern ? (byte)30 : (byte)118,
                    pattern ? (byte)25 : (byte)92);
            }
        }
    }

    private static void SetPixel(
        byte[] pixels,
        int x,
        int y,
        byte blue,
        byte green,
        byte red)
    {
        var offset = (y * Stride) + (x * 4);
        pixels[offset] = blue;
        pixels[offset + 1] = green;
        pixels[offset + 2] = red;
        pixels[offset + 3] = 255;
    }
}
