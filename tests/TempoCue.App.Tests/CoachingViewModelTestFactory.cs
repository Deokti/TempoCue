using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Собирает граф зависимостей коучинга для тестов без дублирования composition root.</summary>
internal static class CoachingViewModelTestFactory
{
    public static CoachingViewModel Create(
        CoachPack pack,
        LocalizationService? localization = null,
        TestCoachPackStore? store = null)
    {
        var resolvedStore = store ?? new TestCoachPackStore();
        return new CoachingViewModel(
            pack,
            localization ?? LocalizationService.Instance,
            TestGamePatchCatalog.Ready(),
            TestChampionCatalog.Ready(),
            TestItemCatalog.Ready(),
            new SaveCoachPackUseCase(resolvedStore),
            new LoadCoachPackLibraryUseCase(resolvedStore),
            new SetCoachPackStateUseCase(resolvedStore));
    }
}
