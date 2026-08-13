using System.IO;
using TempoCue.Coaching;

namespace TempoCue.App.Tests;

public sealed class BuiltInMapControlPackAssetsTests
{
    [Fact]
    public void Assets_ContainIndependentValidBuiltInPacks()
    {
        var directory = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "MapControlPacks");
        var files = Directory.GetFiles(directory, "*.json");

        var packs = files
            .Select(path =>
            {
                using var stream = File.OpenRead(path);
                return MapControlPackLoader.Load(stream);
            })
            .ToArray();

        Assert.Equal(8, packs.Length);
        Assert.Equal(8, packs.Select(static pack => pack.Id).Distinct(
            StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(packs, static pack =>
        {
            Assert.Equal("TempoCue", pack.Author);
            Assert.Equal("ru_RU", pack.ContentLanguage);
            Assert.NotEmpty(pack.Rules.Concat<object>(pack.TimedReminders)
                .Concat(pack.PersistentTips));
        });
        var dragons = Assert.Single(packs, static pack =>
            pack.Id.EndsWith("dragons.ru", StringComparison.Ordinal));
        var voidGrubs = Assert.Single(packs, static pack =>
            pack.Id.EndsWith("void-grubs.ru", StringComparison.Ordinal));
        var riftHerald = Assert.Single(packs, static pack =>
            pack.Id.EndsWith("rift-herald.ru", StringComparison.Ordinal));
        var baron = Assert.Single(packs, static pack =>
            pack.Id.EndsWith("baron-nashor.ru", StringComparison.Ordinal));
        var earlyGank = Assert.Single(packs, static pack =>
            pack.Id.EndsWith("early-gank.ru", StringComparison.Ordinal));
        var riverScuttler = Assert.Single(packs, static pack =>
            pack.Id.EndsWith("river-scuttler.ru", StringComparison.Ordinal));
        var objectiveSetup = Assert.Single(packs, static pack =>
            pack.Id.EndsWith("objective-setup.ru", StringComparison.Ordinal));
        var crossMapTrade = Assert.Single(packs, static pack =>
            pack.Id.EndsWith("cross-map-trade.ru", StringComparison.Ordinal));

        Assert.Equal(MapObjectiveReminderRecurrence.EveryCycle, Assert.Single(dragons.Rules).Recurrence);
        Assert.Equal(MapObjectiveReminderRecurrence.OncePerMatch, Assert.Single(voidGrubs.Rules).Recurrence);
        Assert.Equal(MapObjectiveReminderRecurrence.OncePerMatch, Assert.Single(riftHerald.Rules).Recurrence);
        Assert.Equal(MapObjectiveReminderRecurrence.EveryCycle, Assert.Single(baron.Rules).Recurrence);
        Assert.Equal(TimeSpan.FromMinutes(1.5), Assert.Single(earlyGank.TimedReminders).GameTime);
        Assert.Equal(
            TimeSpan.FromMinutes(2.5),
            Assert.Single(riverScuttler.TimedReminders).GameTime);
        Assert.Equal(
            MapControlPersistentTipScopeKind.WholeMatch,
            Assert.Single(objectiveSetup.PersistentTips).Scope.Kind);
        Assert.Equal(
            MapControlPersistentTipScopeKind.WholeMatch,
            Assert.Single(crossMapTrade.PersistentTips).Scope.Kind);
    }
}
