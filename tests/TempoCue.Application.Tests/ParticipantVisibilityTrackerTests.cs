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

    private static ParticipantVisionObservation Observation(
        string participantId,
        double confidence = 0.85,
        double mapX = 0.4,
        double mapY = 0.6) =>
        new(participantId, confidence, mapX, mapY);
}
