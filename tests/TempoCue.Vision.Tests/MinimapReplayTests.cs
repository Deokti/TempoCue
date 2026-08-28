namespace TempoCue.Vision.Tests;

public sealed class MinimapReplayTests
{
    [Fact]
    public async Task SaveLoadAndRun_PreservesInputAndReplaysLivePipeline()
    {
        var image = CreateRecognizableFrame();
        var reference = MinimapRecognitionTestData.CreateReference(
            image,
            27,
            27,
            "ExampleChampion");
        var references = new[] { reference };
        var pipelineResult = new MinimapRecognitionPipeline(
            new OpenCvMinimapChampionRecognizer()).Analyze(
                image.Pixels,
                image.Width,
                image.Height,
                image.Stride,
                references);
        var sample = new MinimapReplaySample(
            new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.Zero),
            image.Width,
            image.Height,
            image.Stride,
            image.Pixels,
            references,
            new MinimapReplayOutcome(
                pipelineResult.MarkerCandidates,
                pipelineResult.ChampionProposalCount,
                pipelineResult.ChampionCandidates,
                pipelineResult.MaximumIdentityConfidence));
        await using var container = new MemoryStream();

        await MinimapReplaySampleStore.SaveAsync(container, sample);
        container.Position = 0;
        var restored = await MinimapReplaySampleStore.LoadAsync(container);
        var run = new MinimapReplayRunner().Run(restored);

        Assert.Equal(sample.CapturedAt, restored.CapturedAt);
        Assert.Equal(image.Pixels, restored.BgraPixels.ToArray());
        var restoredReference = Assert.Single(restored.ChampionReferences);
        Assert.Equal("ExampleChampion", restoredReference.ChampionId);
        Assert.Equal(reference.BgraPixels.ToArray(), restoredReference.BgraPixels.ToArray());
        Assert.True(run.MatchesBaseline);
        Assert.Equal(0, run.MissingBaselineAssignments);
        Assert.Equal(0, run.UnexpectedAssignments);
        var recognized = Assert.Single(run.Actual.ChampionCandidates);
        Assert.Equal("ExampleChampion", recognized.ChampionId);
    }

    [Fact]
    public void Constructor_CopiesMutableFrameAndBaselineArrays()
    {
        var pixels = new byte[4 * 4 * 4];
        var markerCandidates = new[]
        {
            new MinimapMarkerCandidate(
                1,
                1,
                2,
                2,
                MinimapMarkerAffiliation.Ally,
                0.8)
        };
        var sample = new MinimapReplaySample(
            DateTimeOffset.UnixEpoch,
            4,
            4,
            16,
            pixels,
            [],
            new MinimapReplayOutcome(markerCandidates, 0, [], 0));

        pixels[0] = 255;
        markerCandidates[0] = markerCandidates[0] with { X = 3 };

        Assert.Equal(0, sample.BgraPixels.Span[0]);
        Assert.Equal(1, Assert.Single(sample.Baseline.MarkerCandidates).X);
    }

    private static BgraTestImage CreateRecognizableFrame()
    {
        var image = new BgraTestImage(80, 80, 24, 35, 29);
        image.DrawTexturedCircularMarker(
            centerX: 35,
            centerY: 35,
            innerRadius: 7,
            outerRadius: 9,
            ring: (240, 190, 35),
            firstInterior: (25, 65, 185),
            secondInterior: (180, 215, 225));
        return image;
    }
}
