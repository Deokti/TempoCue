namespace TempoCue.Vision.Tests;

public sealed class MinimapRegionCalculatorTests
{
    [Theory]
    [InlineData(3440, 1440, 2720, 720, 720)]
    [InlineData(1920, 1080, 1380, 540, 540)]
    [InlineData(1280, 720, 920, 360, 360)]
    public void CalculateSearchRegion_ReturnsBottomRightHalfHeightSquare(
        int frameWidth,
        int frameHeight,
        int expectedX,
        int expectedY,
        int expectedSide)
    {
        var region = MinimapRegionCalculator.CalculateSearchRegion(
            frameWidth,
            frameHeight,
            MinimapScreenCorner.BottomRight);

        Assert.Equal(
            new PixelRegion(expectedX, expectedY, expectedSide, expectedSide),
            region);
    }

    [Fact]
    public void CalculateSearchRegion_ReturnsBottomLeftSquare()
    {
        var region = MinimapRegionCalculator.CalculateSearchRegion(
            1920,
            1080,
            MinimapScreenCorner.BottomLeft);

        Assert.Equal(new PixelRegion(0, 540, 540, 540), region);
    }

    [Fact]
    public void CalculateSearchRegion_ClampsSquareToNarrowFrameWidth()
    {
        var region = MinimapRegionCalculator.CalculateSearchRegion(
            100,
            1000,
            MinimapScreenCorner.BottomRight);

        Assert.Equal(new PixelRegion(0, 900, 100, 100), region);
    }
}
