namespace TempoCue.Vision.Tests;

public sealed class MinimapMarkerCandidateDetectorTests
{
    [Fact]
    public void Detect_FindsCompactTeamContoursAndIgnoresDotsAndLines()
    {
        var image = new BgraTestImage(80, 80, 24, 35, 29);
        image.DrawBorder(x: 10, y: 12, width: 12, height: 12, 240, 190, 35);
        image.DrawBorder(x: 46, y: 41, width: 14, height: 14, 45, 55, 235);

        image.DrawFilledRectangle(x: 4, y: 68, width: 3, height: 3, 35, 45, 235);
        image.DrawFilledRectangle(x: 30, y: 5, width: 25, height: 1, 240, 190, 35);

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
}
