namespace TempoCue.Vision.Tests;

public sealed class MinimapChampionMarkerClassifierTests
{
    [Fact]
    public void Classify_KeepsTexturedPortraitAndRejectsHollowTeamSymbol()
    {
        var image = CreatePortraitImage(centerX: 22, centerY: 24);
        BreakTeamRingIntoSeparateArcs(image, centerX: 22, centerY: 24);
        image.DrawBorder(x: 46, y: 41, width: 13, height: 13, 45, 55, 235);
        image.DrawFilledRectangle(x: 49, y: 44, width: 7, height: 7, 18, 22, 24);
        var colorCandidates = DetectColorCandidates(image);
        Assert.NotEmpty(colorCandidates);
        Assert.Contains(colorCandidates, candidate => candidate.X < 40);

        var championCandidates = Classify(image, colorCandidates);

        var champion = Assert.Single(championCandidates);
        Assert.InRange(champion.X, 8, 12);
        Assert.InRange(champion.Y, 10, 14);
        Assert.Equal((25, 25), (champion.Width, champion.Height));
        Assert.Equal(MinimapMarkerAffiliation.Enemy, champion.Affiliation);
        Assert.InRange(champion.Confidence, 0.42, 1);
    }

    [Fact]
    public void Classify_RejectsSmallColorCandidateEvenWithTexturedInterior()
    {
        var image = new BgraTestImage(40, 40, 24, 35, 29);
        image.DrawBorder(x: 10, y: 10, width: 8, height: 8, 240, 190, 35);
        image.DrawCheckerboard(
            x: 12,
            y: 12,
            width: 4,
            height: 4,
            first: (20, 30, 40),
            second: (210, 220, 230));
        var colorCandidates = DetectColorCandidates(image);

        var championCandidates = Classify(image, colorCandidates);

        Assert.Empty(championCandidates);
    }

    [Fact]
    public void Classify_RejectsSparseTeamGlyphOverMapEdge()
    {
        var image = new BgraTestImage(80, 80, 24, 35, 29);
        image.DrawFilledRectangle(x: 34, y: 20, width: 12, height: 40, 105, 120, 112);
        image.DrawBorder(x: 47, y: 36, width: 8, height: 8, 45, 55, 235);
        MinimapMarkerCandidate[] colorCandidates =
        [
            new(
                X: 47,
                Y: 36,
                Width: 8,
                Height: 8,
                Affiliation: MinimapMarkerAffiliation.Enemy,
                Confidence: 0.7)
        ];

        var championCandidates = Classify(image, colorCandidates);

        Assert.Empty(championCandidates);
    }

    private static BgraTestImage CreatePortraitImage(int centerX, int centerY)
    {
        var image = new BgraTestImage(80, 80, 24, 35, 29);
        image.DrawTexturedCircularMarker(
            centerX,
            centerY,
            innerRadius: 9,
            outerRadius: 12,
            ring: (45, 55, 235),
            firstInterior: (35, 65, 170),
            secondInterior: (160, 205, 235));
        return image;
    }

    private static MinimapMarkerCandidate[] DetectColorCandidates(BgraTestImage image) =>
        MinimapMarkerCandidateDetector.Detect(
            image.Pixels,
            image.Width,
            image.Height,
            image.Stride);

    private static MinimapChampionMarkerCandidate[] Classify(
        BgraTestImage image,
        IReadOnlyList<MinimapMarkerCandidate> colorCandidates) =>
        MinimapChampionMarkerClassifier.Classify(
            image.Pixels,
            image.Width,
            image.Height,
            image.Stride,
            colorCandidates);

    private static void BreakTeamRingIntoSeparateArcs(
        BgraTestImage image,
        int centerX,
        int centerY)
    {
        const byte backgroundBlue = 24;
        const byte backgroundGreen = 35;
        const byte backgroundRed = 29;
        image.DrawFilledRectangle(
            centerX - 2,
            centerY - 12,
            width: 5,
            height: 5,
            backgroundBlue,
            backgroundGreen,
            backgroundRed);
        image.DrawFilledRectangle(
            centerX - 2,
            centerY + 8,
            width: 5,
            height: 5,
            backgroundBlue,
            backgroundGreen,
            backgroundRed);
        image.DrawFilledRectangle(
            centerX - 12,
            centerY - 2,
            width: 5,
            height: 5,
            backgroundBlue,
            backgroundGreen,
            backgroundRed);
        image.DrawFilledRectangle(
            centerX + 8,
            centerY - 2,
            width: 5,
            height: 5,
            backgroundBlue,
            backgroundGreen,
            backgroundRed);
    }
}
