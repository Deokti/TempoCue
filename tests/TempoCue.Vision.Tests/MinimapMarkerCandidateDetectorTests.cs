namespace TempoCue.Vision.Tests;

public sealed class MinimapMarkerCandidateDetectorTests
{
    [Fact]
    public void Detect_FindsCompactTeamContoursAndIgnoresDotsAndLines()
    {
        var image = new BgraTestImage(80, 80, 24, 35, 29);
        DrawBorder(image, x: 10, y: 12, width: 12, height: 12, 240, 190, 35);
        DrawBorder(image, x: 46, y: 41, width: 14, height: 14, 45, 55, 235);

        DrawFilledRectangle(image, x: 4, y: 68, width: 3, height: 3, 35, 45, 235);
        DrawFilledRectangle(image, x: 30, y: 5, width: 25, height: 1, 240, 190, 35);

        var candidates = MinimapMarkerCandidateDetector.Detect(
            image.Pixels,
            image.Width,
            image.Height,
            image.Stride);

        var ally = Assert.Single(candidates, candidate =>
            candidate.Affiliation == MinimapMarkerAffiliation.Ally);
        Assert.Equal((10, 12, 12, 12), (ally.X, ally.Y, ally.Width, ally.Height));
        Assert.InRange(ally.Confidence, 0.6, 1);

        var enemy = Assert.Single(candidates, candidate =>
            candidate.Affiliation == MinimapMarkerAffiliation.Enemy);
        Assert.Equal((46, 41, 14, 14), (enemy.X, enemy.Y, enemy.Width, enemy.Height));
        Assert.InRange(enemy.Confidence, 0.6, 1);
    }

    [Fact]
    public void Detect_RejectsIncompleteImage()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            MinimapMarkerCandidateDetector.Detect(
                new byte[63],
                width: 4,
                height: 4,
                stride: 16));

        Assert.Equal("pixels", exception.ParamName);
    }

    private static void DrawBorder(
        BgraTestImage image,
        int x,
        int y,
        int width,
        int height,
        byte blue,
        byte green,
        byte red)
    {
        for (var offsetX = 0; offsetX < width; offsetX++)
        {
            image.SetPixel(x + offsetX, y, blue, green, red);
            image.SetPixel(x + offsetX, y + height - 1, blue, green, red);
        }

        for (var offsetY = 1; offsetY < height - 1; offsetY++)
        {
            image.SetPixel(x, y + offsetY, blue, green, red);
            image.SetPixel(x + width - 1, y + offsetY, blue, green, red);
        }
    }

    private static void DrawFilledRectangle(
        BgraTestImage image,
        int x,
        int y,
        int width,
        int height,
        byte blue,
        byte green,
        byte red)
    {
        for (var offsetY = 0; offsetY < height; offsetY++)
        {
            for (var offsetX = 0; offsetX < width; offsetX++)
            {
                image.SetPixel(x + offsetX, y + offsetY, blue, green, red);
            }
        }
    }
}
