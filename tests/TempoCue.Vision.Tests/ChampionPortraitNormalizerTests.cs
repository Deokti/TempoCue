namespace TempoCue.Vision.Tests;

public sealed class ChampionPortraitNormalizerTests
{
    [Fact]
    public void NormalizeBgra32_PreservesVisiblePixelsInsteadOfReturningBlackReference()
    {
        var source = new BgraTestImage(120, 120, blue: 15, green: 70, red: 180);
        source.DrawCheckerboard(
            20,
            20,
            80,
            80,
            (Blue: 210, Green: 30, Red: 45),
            (Blue: 35, Green: 190, Red: 125));

        var normalized = ChampionPortraitNormalizer.NormalizeBgra32(
            source.Pixels,
            source.Width,
            source.Height,
            source.Stride);

        Assert.Equal(
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.ImageSide *
            MinimapChampionReference.BytesPerPixel,
            normalized.Length);
        Assert.Contains(normalized, static value => value > 0);
        Assert.All(
            Enumerable.Range(0, MinimapChampionReference.ImageSide * MinimapChampionReference.ImageSide),
            pixelIndex => Assert.Equal(255, normalized[(pixelIndex * 4) + 3]));
    }

    [Fact]
    public void NormalizeBgra32_CropsRectangularImageAroundItsCenter()
    {
        var source = new BgraTestImage(6, 4, blue: 0, green: 0, red: 0);
        source.DrawFilledRectangle(1, 0, 4, 4, blue: 40, green: 90, red: 160);

        var normalized = ChampionPortraitNormalizer.NormalizeBgra32(
            source.Pixels,
            source.Width,
            source.Height,
            source.Stride);

        Assert.All(
            Enumerable.Range(0, MinimapChampionReference.ImageSide * MinimapChampionReference.ImageSide),
            pixelIndex =>
            {
                var offset = pixelIndex * 4;
                Assert.Equal(40, normalized[offset]);
                Assert.Equal(90, normalized[offset + 1]);
                Assert.Equal(160, normalized[offset + 2]);
                Assert.Equal(255, normalized[offset + 3]);
            });
    }
}
