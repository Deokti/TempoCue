using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.Application.Tests;

public sealed class ExportCoachPackUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_WritesPortablePackThatCanBeLoadedAgain()
    {
        var pack = new CoachPack
        {
            SchemaVersion = 2,
            Id = "matchup.urgot-vs-sett.export",
            Name = "Urgot против Sett",
            Patch = "26.15",
            Description = "Проверка переносимого Coach Pack.",
            Author = "TestAuthor",
            ContentLanguage = "ru-RU",
            PlayerChampion = "Urgot",
            OpponentChampion = "Sett",
            LaneRole = "top",
            VerifiedOn = new DateOnly(2026, 8, 11),
            Source = new CoachPackSource
            {
                Kind = CoachPackSourceKind.PersonalExperience,
                Permission = CoachPackSourcePermission.NotRequired
            },
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
                    Message = "Не начинай затяжной бой.",
                    Source = "Личный опыт"
                }
            ]
        };
        var useCase = new ExportCoachPackUseCase();
        await using var destination = new MemoryStream();

        await useCase.ExecuteAsync(pack, destination);

        destination.Position = 0;
        var loaded = await CoachPackLoader.LoadAsync(destination);
        Assert.Equal(pack.Id, loaded.Id);
        Assert.Equal(pack.Name, loaded.Name);
        Assert.Equal(pack.Source, loaded.Source);
        var loadedRule = Assert.Single(loaded.Rules);
        Assert.Equal(pack.Rules[0].Id, loadedRule.Id);
        Assert.Equal(pack.Rules[0].Title, loadedRule.Title);
        Assert.Equal(pack.Rules[0].Message, loadedRule.Message);
        Assert.Equal(pack.Rules[0].RequiredItemIds, loadedRule.RequiredItemIds);
        Assert.Equal(pack.Rules[0].ExcludedItemIds, loadedRule.ExcludedItemIds);
        Assert.Equal(pack.Rules[0].RecommendedItemIds, loadedRule.RecommendedItemIds);
        Assert.Empty(loaded.PersistentTips);
        Assert.Empty(loaded.ItemBlocks);
    }
}
