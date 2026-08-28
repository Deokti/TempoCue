using TempoCue.Vision;

namespace TempoCue.Vision.Tests;

public sealed class MinimapIconRingSamplerTests
{
    [Fact]
    public void Sample_CountsTeamAndBrightPixelsOnIconRing()
    {
        var image = new BgraTestImage(64, 64, 24, 35, 29);
        var proposal = new MinimapChampionMarkerCandidate(
            X: 14,
            Y: 14,
            Width: 25,
            Height: 25,
            MinimapMarkerAffiliation.Enemy,
            Confidence: 0.7);
        // Красное кольцо врага на радиусе значка и одна яркая точка свечения.
        DrawRingPixels(image, centerX: 26, centerY: 26, radius: 11, 30, 40, 220);
        image.SetPixel(26, 15, 235, 235, 235);

        var stats = MinimapIconRingSampler.Sample(
            image.Pixels,
            image.Width,
            image.Height,
            image.Stride,
            proposal);

        Assert.True(stats.SampledPixels > 100);
        Assert.True(stats.EnemyColored >= 30);
        Assert.Equal(0, stats.AllyColored);
        Assert.True(stats.BrightPixels >= 1);
    }

    private static void DrawRingPixels(
        BgraTestImage image,
        int centerX,
        int centerY,
        int radius,
        byte blue,
        byte green,
        byte red)
    {
        for (var y = centerY - radius - 1; y <= centerY + radius + 1; y++)
        {
            for (var x = centerX - radius - 1; x <= centerX + radius + 1; x++)
            {
                var deltaX = x - centerX;
                var deltaY = y - centerY;
                var distance = Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY));
                if (distance >= radius - 1 && distance <= radius + 1)
                {
                    image.SetPixel(x, y, blue, green, red);
                }
            }
        }
    }
}
