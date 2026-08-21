namespace TempoCue.Vision.Tests;

public sealed class MinimapChampionIdentityMatcherTests
{
    [Fact]
    public void Match_KeepsCandidateMatchingParticipantPortraitAndRejectsStructure()
    {
        var image = new BgraTestImage(80, 50, 20, 30, 25);
        image.DrawTexturedCircularMarker(
            centerX: 18,
            centerY: 22,
            innerRadius: 9,
            outerRadius: 12,
            ring: (210, 155, 35),
            firstInterior: (25, 65, 185),
            secondInterior: (180, 215, 225));
        image.DrawBorder(x: 51, y: 16, width: 9, height: 9, 210, 155, 35);
        image.DrawFilledRectangle(x: 35, y: 7, width: 4, height: 36, 85, 105, 95);
        MinimapChampionMarkerCandidate[] candidates =
        [
            new(6, 10, 25, 25, MinimapMarkerAffiliation.Ally, 0.72),
            new(43, 8, 25, 25, MinimapMarkerAffiliation.Ally, 0.68)
        ];
        var reference = MinimapRecognitionTestData.CreateReference(
            image,
            startX: 10,
            startY: 14,
            "ExampleChampion");

        var matches = Match(image, candidates, reference);

        var match = Assert.Single(matches);
        Assert.Equal("ExampleChampion", match.ChampionId);
        Assert.InRange(match.IdentityConfidence, 0.99, 1);
        Assert.Equal((6, 10), (match.X, match.Y));
    }

    [Fact]
    public void Match_DoesNotCompareCandidateWithOpposingTeamReferences()
    {
        var image = new BgraTestImage(40, 40, 20, 30, 25);
        image.DrawCheckerboard(
            x: 10,
            y: 10,
            width: MinimapChampionReference.ImageSide,
            height: MinimapChampionReference.ImageSide,
            first: (25, 65, 185),
            second: (180, 215, 225));
        var reference = MinimapRecognitionTestData.CreateReference(
            image,
            10,
            10,
            "EnemyChampion",
            MinimapMarkerAffiliation.Enemy);
        MinimapChampionMarkerCandidate[] candidates =
        [
            new(6, 6, 25, 25, MinimapMarkerAffiliation.Ally, 0.72)
        ];

        var matches = Match(image, candidates, reference);

        Assert.Empty(matches);
    }

    [Fact]
    public void Match_RecognizesPortraitRenderedAtDifferentMinimapScale()
    {
        var referenceImage = new BgraTestImage(17, 17, 18, 24, 30);
        for (var y = 0; y < referenceImage.Height; y++)
        {
            for (var x = 0; x < referenceImage.Width; x++)
            {
                referenceImage.SetPixel(
                    x,
                    y,
                    (byte)(20 + (x * 9)),
                    (byte)(35 + (y * 7)),
                    (byte)(45 + (((x * 3) + (y * 5)) % 120)));
            }
        }

        var reference = MinimapRecognitionTestData.CreateReference(
            referenceImage,
            0,
            0,
            "ScaledChampion");
        var observedImage = new BgraTestImage(50, 50, 15, 20, 25);
        DrawScaledReference(observedImage, reference, startX: 18, startY: 18, side: 13);
        MinimapChampionMarkerCandidate[] candidates =
        [
            new(12, 12, 25, 25, MinimapMarkerAffiliation.Ally, 0.72)
        ];

        var match = Assert.Single(Match(observedImage, candidates, reference));

        Assert.Equal("ScaledChampion", match.ChampionId);
        Assert.InRange(match.IdentityConfidence, 0.9, 1);
    }

    [Fact]
    public void Match_RecognizesPortraitWhenColorArcIsOffsetFromPortraitCenter()
    {
        var image = new BgraTestImage(60, 50, 20, 30, 25);
        image.DrawCheckerboard(
            x: 24,
            y: 16,
            width: MinimapChampionReference.ImageSide,
            height: MinimapChampionReference.ImageSide,
            first: (25, 65, 185),
            second: (180, 215, 225));
        var reference = MinimapRecognitionTestData.CreateReference(
            image,
            24,
            16,
            "OffsetChampion");
        MinimapChampionMarkerCandidate[] candidates =
        [
            new(12, 12, 25, 25, MinimapMarkerAffiliation.Ally, 0.72)
        ];

        var match = Assert.Single(Match(image, candidates, reference));

        Assert.Equal("OffsetChampion", match.ChampionId);
        Assert.InRange(match.IdentityConfidence, 0.99, 1);
    }

    private static MinimapChampionMarkerCandidate[] Match(
        BgraTestImage image,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        params MinimapChampionReference[] references) =>
        MinimapChampionIdentityMatcher.Match(
            image.Pixels,
            image.Width,
            image.Height,
            image.Stride,
            candidates,
            references);

    private static void DrawScaledReference(
        BgraTestImage image,
        MinimapChampionReference reference,
        int startX,
        int startY,
        int side)
    {
        var referencePixels = reference.BgraPixels.Span;
        for (var y = 0; y < side; y++)
        {
            for (var x = 0; x < side; x++)
            {
                var referenceX = (int)Math.Round(
                    x * (MinimapChampionReference.ImageSide - 1d) / (side - 1d));
                var referenceY = (int)Math.Round(
                    y * (MinimapChampionReference.ImageSide - 1d) / (side - 1d));
                var offset = ((referenceY * MinimapChampionReference.ImageSide) + referenceX) *
                    MinimapChampionReference.BytesPerPixel;
                image.SetPixel(
                    startX + x,
                    startY + y,
                    referencePixels[offset],
                    referencePixels[offset + 1],
                    referencePixels[offset + 2]);
            }
        }
    }
}
