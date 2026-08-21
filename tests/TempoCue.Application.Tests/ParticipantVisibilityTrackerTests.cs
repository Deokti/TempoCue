using TempoCue.Application;

namespace TempoCue.Application.Tests;

public sealed class ParticipantVisibilityTrackerTests
{
    private static readonly DateTimeOffset Start =
        new(2026, 8, 21, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Update_RequiresTwoConsecutiveDetectionsBeforeParticipantIsVisible()
    {
        var tracker = new ParticipantVisibilityTracker();

        var first = Assert.Single(tracker.Update(
            Start,
            ["enemy-jungle"],
            [Observation("enemy-jungle", 0.81)]));
        var second = Assert.Single(tracker.Update(
            Start.AddMilliseconds(250),
            ["enemy-jungle"],
            [Observation("enemy-jungle", 0.86)]));

        Assert.Equal(ParticipantVisibilityStatus.Confirming, first.Status);
        Assert.Null(first.LastSeenAt);
        Assert.Equal(ParticipantVisibilityStatus.Visible, second.Status);
        Assert.Equal(Start.AddMilliseconds(250), second.LastSeenAt);
        Assert.Equal(0.86, second.Confidence);
    }

    [Fact]
    public void Update_KeepsVisibleDuringBriefMissAndThenPublishesLastSeen()
    {
        var tracker = new ParticipantVisibilityTracker();
        _ = tracker.Update(Start, ["enemy-jungle"], [Observation("enemy-jungle")]);
        _ = tracker.Update(
            Start.AddMilliseconds(250),
            ["enemy-jungle"],
            [Observation("enemy-jungle")]);

        var briefMiss = Assert.Single(tracker.Update(
            Start.AddMilliseconds(750),
            ["enemy-jungle"],
            []));
        var expired = Assert.Single(tracker.Update(
            Start.AddSeconds(1.5),
            ["enemy-jungle"],
            []));

        Assert.Equal(ParticipantVisibilityStatus.Visible, briefMiss.Status);
        Assert.Equal(ParticipantVisibilityStatus.NotVisible, expired.Status);
        Assert.Equal(Start.AddMilliseconds(250), expired.LastSeenAt);
    }

    [Fact]
    public void Update_ConfirmsVisibilityWhenMissedFrameSeparatesDetectionsWithinGap()
    {
        var tracker = new ParticipantVisibilityTracker();

        _ = tracker.Update(Start, ["enemy-jungle"], [Observation("enemy-jungle")]);
        _ = tracker.Update(Start.AddMilliseconds(250), ["enemy-jungle"], []);
        var confirmed = Assert.Single(tracker.Update(
            Start.AddMilliseconds(500),
            ["enemy-jungle"],
            [Observation("enemy-jungle")]));

        Assert.Equal(ParticipantVisibilityStatus.Visible, confirmed.Status);
        Assert.Equal(Start.AddMilliseconds(500), confirmed.LastSeenAt);
    }

    [Fact]
    public void Update_DoesNotConfirmVisibilityWhenDetectionGapExceedsLimit()
    {
        var tracker = new ParticipantVisibilityTracker();

        _ = tracker.Update(Start, ["enemy-jungle"], [Observation("enemy-jungle")]);
        var late = Assert.Single(tracker.Update(
            Start.AddMilliseconds(700),
            ["enemy-jungle"],
            [Observation("enemy-jungle")]));

        Assert.Equal(ParticipantVisibilityStatus.Confirming, late.Status);
        Assert.Null(late.LastSeenAt);
    }

    [Fact]
    public void Update_ConfirmsVisibilityAtExactDetectionGapBoundary()
    {
        var tracker = new ParticipantVisibilityTracker();

        _ = tracker.Update(Start, ["enemy-jungle"], [Observation("enemy-jungle")]);
        var boundary = Assert.Single(tracker.Update(
            Start.AddMilliseconds(600),
            ["enemy-jungle"],
            [Observation("enemy-jungle")]));

        Assert.Equal(ParticipantVisibilityStatus.Visible, boundary.Status);
    }

    [Fact]
    public void Update_KeepsVisibleAtExactGracePeriodBoundary()
    {
        var tracker = new ParticipantVisibilityTracker();
        _ = tracker.Update(Start, ["enemy-jungle"], [Observation("enemy-jungle")]);
        _ = tracker.Update(
            Start.AddMilliseconds(250),
            ["enemy-jungle"],
            [Observation("enemy-jungle")]);

        var atBoundary = Assert.Single(tracker.Update(
            Start.AddMilliseconds(1250),
            ["enemy-jungle"],
            []));
        var pastBoundary = Assert.Single(tracker.Update(
            Start.AddMilliseconds(1251),
            ["enemy-jungle"],
            []));

        Assert.Equal(ParticipantVisibilityStatus.Visible, atBoundary.Status);
        Assert.Equal(ParticipantVisibilityStatus.NotVisible, pastBoundary.Status);
    }

    [Fact]
    public void Update_RequiresFreshConfirmationAfterVisibilityExpires()
    {
        var tracker = new ParticipantVisibilityTracker();
        _ = tracker.Update(Start, ["enemy-jungle"], [Observation("enemy-jungle")]);
        _ = tracker.Update(
            Start.AddMilliseconds(250),
            ["enemy-jungle"],
            [Observation("enemy-jungle")]);
        _ = tracker.Update(Start.AddSeconds(5), ["enemy-jungle"], []);

        var reappeared = Assert.Single(tracker.Update(
            Start.AddSeconds(10),
            ["enemy-jungle"],
            [Observation("enemy-jungle")]));

        Assert.Equal(ParticipantVisibilityStatus.Confirming, reappeared.Status);
    }

    [Fact]
    public void Update_RemovesParticipantThatIsNoLongerExpected()
    {
        var tracker = new ParticipantVisibilityTracker(requiredConsecutiveDetections: 1);
        _ = tracker.Update(Start, ["old-player"], [Observation("old-player")]);

        var replacement = Assert.Single(tracker.Update(
            Start.AddMilliseconds(250),
            ["new-player"],
            []));
        var oldAgain = Assert.Single(tracker.Update(
            Start.AddMilliseconds(500),
            ["old-player"],
            []));

        Assert.Equal("new-player", replacement.ParticipantId);
        Assert.Equal(ParticipantVisibilityStatus.NotVisible, replacement.Status);
        Assert.Null(oldAgain.LastSeenAt);
    }

    [Fact]
    public void Update_UsesMostConfidentObservationForSameParticipant()
    {
        var tracker = new ParticipantVisibilityTracker(requiredConsecutiveDetections: 1);

        var result = Assert.Single(tracker.Update(
            Start,
            ["enemy-mid"],
            [
                Observation("enemy-mid", 0.77, 0.2, 0.3),
                Observation("enemy-mid", 0.91, 0.8, 0.7)
            ]));

        Assert.Equal(0.91, result.Confidence);
        Assert.Equal(0.8, result.MapX);
        Assert.Equal(0.7, result.MapY);
    }

    [Fact]
    public void Update_LowConfidenceDetectionsDoNotOccupyVisibleState()
    {
        var tracker = new ParticipantVisibilityTracker();

        var first = Assert.Single(tracker.Update(
            Start,
            ["enemy-jungle"],
            [Observation("enemy-jungle", 0.66)]));
        var repeated = Assert.Single(tracker.Update(
            Start.AddMilliseconds(250),
            ["enemy-jungle"],
            [Observation("enemy-jungle", 0.66)]));
        var realAppearanceStart = Assert.Single(tracker.Update(
            Start.AddMilliseconds(500),
            ["enemy-jungle"],
            [Observation("enemy-jungle", 0.90)]));
        var realAppearanceConfirmed = Assert.Single(tracker.Update(
            Start.AddMilliseconds(750),
            ["enemy-jungle"],
            [Observation("enemy-jungle", 0.91)]));

        Assert.Equal(ParticipantVisibilityStatus.NotVisible, first.Status);
        Assert.Equal(ParticipantVisibilityStatus.NotVisible, repeated.Status);
        Assert.Equal(ParticipantVisibilityStatus.Confirming, realAppearanceStart.Status);
        Assert.Equal(ParticipantVisibilityStatus.Visible, realAppearanceConfirmed.Status);
    }

    [Fact]
    public void Constructor_RejectsInvalidMinimumObservationConfidence()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ParticipantVisibilityTracker(minimumObservationConfidence: -0.01));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ParticipantVisibilityTracker(minimumObservationConfidence: 1.01));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ParticipantVisibilityTracker(minimumObservationConfidence: double.NaN));
    }

    private static ParticipantVisionObservation Observation(
        string participantId,
        double confidence = 0.85,
        double mapX = 0.4,
        double mapY = 0.6) =>
        new(participantId, confidence, mapX, mapY);
}
