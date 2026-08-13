using TempoCue.Core;

namespace TempoCue.Coaching.Tests;

public sealed class MapObjectiveTimingCatalogTests
{
    [Theory]
    [InlineData("16.15")]
    [InlineData("16.15.1")]
    [InlineData("26.15")]
    public void CurrentPatch_ReturnsVerifiedSummonersRiftTimings(string patch)
    {
        var found = MapObjectiveTimingCatalog.TryGetForPatch(patch, out var timingSet);

        Assert.True(found);
        Assert.NotNull(timingSet);
        Assert.Equal("16.15", timingSet.RequestedPatch);
        Assert.Equal("16.1", timingSet.EffectiveFromPatch);
        Assert.Equal("16.15", timingSet.VerifiedThroughPatch);
        Assert.True(timingSet.IsVerifiedForRequestedPatch);
        Assert.False(timingSet.IsUsingPreviousPatchData);
        AssertTiming(timingSet, MapObjectiveKind.ElementalDragon, 5, 5);
        AssertTiming(timingSet, MapObjectiveKind.VoidGrubs, 8, null);
        AssertTiming(timingSet, MapObjectiveKind.RiftHerald, 15, null);
        AssertTiming(timingSet, MapObjectiveKind.BaronNashor, 20, 6);
    }

    [Fact]
    public void PatchBeforeSeason2026_UsesPreviousBaronSpawnTime()
    {
        var found = MapObjectiveTimingCatalog.TryGetForPatch("15.24", out var timingSet);

        Assert.True(found);
        Assert.NotNull(timingSet);
        Assert.True(timingSet.IsVerifiedForRequestedPatch);
        Assert.Equal("15.9", timingSet.EffectiveFromPatch);
        AssertTiming(timingSet, MapObjectiveKind.BaronNashor, 25, 6);
    }

    [Theory]
    [InlineData("16.16")]
    [InlineData("26.16")]
    [InlineData("27.1")]
    public void FuturePatch_UsesLastVerifiedTimingsWithWarning(string patch)
    {
        var found = MapObjectiveTimingCatalog.TryGetForPatch(patch, out var timingSet);

        Assert.True(found);
        Assert.NotNull(timingSet);
        Assert.Equal("16.1", timingSet.EffectiveFromPatch);
        Assert.Equal("16.15", timingSet.VerifiedThroughPatch);
        Assert.False(timingSet.IsVerifiedForRequestedPatch);
        Assert.True(timingSet.IsUsingPreviousPatchData);
        AssertTiming(timingSet, MapObjectiveKind.BaronNashor, 20, 6);
    }

    [Theory]
    [InlineData("15.8")]
    [InlineData("unknown")]
    [InlineData("")]
    public void MalformedOrOlderUnsupportedPatch_IsRejected(string patch)
    {
        var found = MapObjectiveTimingCatalog.TryGetForPatch(patch, out var timingSet);

        Assert.False(found);
        Assert.Null(timingSet);
    }

    private static void AssertTiming(
        MapObjectiveTimingSet timingSet,
        MapObjectiveKind objective,
        int initialSpawnMinutes,
        int? respawnMinutes)
    {
        var timing = timingSet.Get(objective);
        Assert.Equal(TimeSpan.FromMinutes(initialSpawnMinutes), timing.InitialSpawnTime);
        Assert.Equal(
            respawnMinutes is null ? null : TimeSpan.FromMinutes(respawnMinutes.Value),
            timing.RespawnDelay);
        Assert.Equal(respawnMinutes is not null, timing.Respawns);
    }
}
