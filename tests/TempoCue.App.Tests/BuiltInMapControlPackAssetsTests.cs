using System.IO;
using TempoCue.Coaching;
using TempoCue.Core;

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

        // Восемь наборов про нейтральные объекты и пять про наблюдение за
        // соперниками (ADR 0016).
        Assert.Equal(13, packs.Length);
        Assert.Equal(13, packs.Select(static pack => pack.Id).Distinct(
            StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(packs, static pack =>
        {
            Assert.Equal("TempoCue", pack.Author);
            Assert.Equal("ru_RU", pack.ContentLanguage);
            Assert.NotEmpty(pack.Rules.Concat<object>(pack.TimedReminders)
                .Concat(pack.PersistentTips)
                .Concat(pack.VisionRules));
        });

        // Наблюдение поставляется на появление лесника и на пропажу каждой из
        // четырёх остальных ролей. Остальные восемь наборов про объекты и правил
        // наблюдения не содержат.
        var visionPacks = packs.Where(static pack => pack.VisionRules.Count > 0).ToArray();
        Assert.Equal(5, visionPacks.Length);
        Assert.Single(visionPacks, static pack => pack.VisionRules.Any(
            static rule => rule.Position is PlayerPosition.Jungle
                && rule.Transition is MapVisionTransition.Appeared));
        foreach (var position in new[]
                 {
                     PlayerPosition.Top,
                     PlayerPosition.Middle,
                     PlayerPosition.Bottom,
                     PlayerPosition.Support
                 })
        {
            Assert.Single(visionPacks, pack => pack.VisionRules.Any(
                rule => rule.Position == position
                    && rule.Transition is MapVisionTransition.Disappeared));
        }

        // Каждое правило наблюдения подставляет имя чемпиона: без метки совет
        // говорил бы «кто-то пропал», а игроку нужно знать кто.
        Assert.All(
            visionPacks.SelectMany(static pack => pack.VisionRules),
            static rule => Assert.Contains("{champion}", rule.Message, StringComparison.Ordinal));
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
