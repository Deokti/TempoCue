namespace TempoCue.Vision.Tests;

public sealed class OpenCvMinimapChampionRecognizerTests
{
    [Fact]
    public void Recognize_MatchesColorPortraitAndRejectsFlatStructure()
    {
        var image = new BgraTestImage(90, 50, 20, 30, 25);
        image.DrawCheckerboard(
            x: 10,
            y: 14,
            width: MinimapChampionReference.ImageSide,
            height: MinimapChampionReference.ImageSide,
            first: (25, 65, 185),
            second: (180, 215, 225));
        image.DrawBorder(x: 60, y: 15, width: 12, height: 12, 210, 155, 35);
        var reference = MinimapRecognitionTestData.CreateReference(
            image,
            10,
            14,
            "ExampleChampion");
        MinimapChampionMarkerCandidate[] candidates =
        [
            new(6, 10, 25, 25, MinimapMarkerAffiliation.Ally, 0.72),
            new(54, 9, 25, 25, MinimapMarkerAffiliation.Ally, 0.68)
        ];

        var result = Recognize(image, candidates, reference);

        var match = Assert.Single(result.Candidates);
        Assert.Equal("ExampleChampion", match.ChampionId);
        Assert.Equal((6, 10), (match.X, match.Y));
        Assert.InRange(match.IdentityConfidence, 0.99, 1);
    }

    [Fact]
    public void Recognize_AssignsParticipantOnlyOnceForDuplicateProposals()
    {
        var image = new BgraTestImage(60, 50, 20, 30, 25);
        image.DrawCheckerboard(
            x: 20,
            y: 16,
            width: MinimapChampionReference.ImageSide,
            height: MinimapChampionReference.ImageSide,
            first: (25, 65, 185),
            second: (180, 215, 225));
        var reference = MinimapRecognitionTestData.CreateReference(
            image,
            20,
            16,
            "ExampleChampion");
        MinimapChampionMarkerCandidate[] candidates =
        [
            new(14, 10, 25, 25, MinimapMarkerAffiliation.Ally, 0.72),
            new(17, 12, 25, 25, MinimapMarkerAffiliation.Ally, 0.70)
        ];

        var result = Recognize(image, candidates, reference);

        var match = Assert.Single(result.Candidates);
        Assert.Equal("ExampleChampion", match.ChampionId);
    }

    [Fact]
    public void Recognize_UsesRosterAffiliationWhenContourColorIsWrong()
    {
        var image = new BgraTestImage(50, 50, 20, 30, 25);
        image.DrawCheckerboard(
            x: 16,
            y: 16,
            width: MinimapChampionReference.ImageSide,
            height: MinimapChampionReference.ImageSide,
            first: (25, 65, 185),
            second: (180, 215, 225));
        var enemyReference = MinimapRecognitionTestData.CreateReference(
            image,
            16,
            16,
            "EnemyChampion",
            MinimapMarkerAffiliation.Enemy);
        MinimapChampionMarkerCandidate[] candidates =
        [
            new(12, 12, 25, 25, MinimapMarkerAffiliation.Ally, 0.72)
        ];

        var result = Recognize(image, candidates, enemyReference);

        var match = Assert.Single(result.Candidates);
        Assert.Equal("EnemyChampion", match.ChampionId);
        Assert.Equal(MinimapMarkerAffiliation.Enemy, match.Affiliation);
        Assert.Equal(MinimapMarkerAffiliation.Ally, match.ContourAffiliation);
    }

    [Fact]
    public void Recognize_UsesContourAffiliationForMirroredChampion()
    {
        var image = new BgraTestImage(50, 50, 20, 30, 25);
        image.DrawCheckerboard(
            x: 16,
            y: 16,
            width: MinimapChampionReference.ImageSide,
            height: MinimapChampionReference.ImageSide,
            first: (25, 65, 185),
            second: (180, 215, 225));
        var allyReference = MinimapRecognitionTestData.CreateReference(
            image,
            16,
            16,
            "MirrorChampion",
            MinimapMarkerAffiliation.Ally);
        var enemyReference = MinimapRecognitionTestData.CreateReference(
            image,
            16,
            16,
            "MirrorChampion",
            MinimapMarkerAffiliation.Enemy);
        MinimapChampionMarkerCandidate[] candidates =
        [
            new(12, 12, 25, 25, MinimapMarkerAffiliation.Enemy, 0.72)
        ];

        var result = Recognize(image, candidates, allyReference, enemyReference);

        var match = Assert.Single(result.Candidates);
        Assert.Equal(MinimapMarkerAffiliation.Enemy, match.Affiliation);
        Assert.Equal(MinimapMarkerAffiliation.Enemy, match.ContourAffiliation);
    }

    private static MinimapChampionRecognitionResult Recognize(
        BgraTestImage image,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        params MinimapChampionReference[] references) =>
        new OpenCvMinimapChampionRecognizer().Recognize(
            image.Pixels,
            image.Width,
            image.Height,
            image.Stride,
            candidates,
            references);

}
