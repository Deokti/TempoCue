using TempoCue.Core;

namespace TempoCue.Core.Tests;

public sealed class CoachPackWriterTests
{
    [Fact]
    public async Task VersionTwo_RoundTripsAllMatchupContent()
    {
        var source = new CoachPackSource
        {
            Kind = CoachPackSourceKind.CommunityGuide,
            Title = "Urgot matchup guide",
            Url = "https://example.com/guide",
            Author = "GuideAuthor",
            Permission = CoachPackSourcePermission.NotConfirmed
        };
        var pack = new CoachPack
        {
            SchemaVersion = 2,
            Id = "matchup.urgot-vs-sett.test",
            Name = "Urgot против Sett",
            Patch = "26.15",
            Description = "Проверяем полное сохранение нового формата.",
            Author = "TempoCueAuthor",
            ContentLanguage = "ru-RU",
            PlayerChampion = "Urgot",
            OpponentChampion = "Sett",
            LaneRole = "top",
            VerifiedOn = new DateOnly(2026, 8, 10),
            Source = source,
            Rules =
            [
                new CoachRuleDefinition
                {
                    Id = "phase-1",
                    PlayerChampion = "Urgot",
                    OpponentChampion = "Sett",
                    MinimumLevel = 1,
                    MaximumLevel = 8,
                    PowerState = PowerState.Dangerous,
                    Priority = 1000,
                    Title = "Осторожное начало",
                    Message = "Не ищи размен первым",
                    WhyNow = "Sett сильнее",
                    NextGoal = "Дождаться девятого уровня",
                    RecommendedItemIds = [1054],
                    Source = "Urgot matchup guide — GuideAuthor"
                }
            ],
            PersistentTips =
            [
                new CoachPersistentTipDefinition
                {
                    Id = "tip-1",
                    Title = "Следи за W",
                    Message = "Не стой в центре умения.",
                    ApplicableRuleId = "phase-1"
                }
            ],
            ItemBlocks =
            [
                new CoachItemBlockDefinition
                {
                    Id = "block-1",
                    Name = "Старт",
                    WhenToChoose = "Обычная линия",
                    ItemIds = [1054, 2003]
                }
            ]
        };

        await using var stream = new MemoryStream();
        await CoachPackWriter.WriteAsync(stream, pack);
        stream.Position = 0;
        var loaded = CoachPackLoader.Load(stream);

        Assert.Equal(2, loaded.SchemaVersion);
        Assert.Equal(source, loaded.Source);
        Assert.Equal([1054], loaded.Rules[0].RecommendedItemIds);
        Assert.Single(loaded.PersistentTips);
        Assert.Equal([1054, 2003], loaded.ItemBlocks[0].ItemIds);
    }
}
