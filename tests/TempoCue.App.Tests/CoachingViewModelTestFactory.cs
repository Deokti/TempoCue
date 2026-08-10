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
        var resolvedLocalization = localization ?? LocalizationService.Instance;
        var championCatalog = TestChampionCatalog.Ready();
        var library = new CoachingLibraryViewModel(
            pack,
            resolvedLocalization,
            championCatalog,
            new LoadCoachPackLibraryUseCase(resolvedStore),
            new SetCoachPackStateUseCase(resolvedStore));
        var editor = new MatchupEditorViewModel(
            resolvedLocalization,
            TestGamePatchCatalog.Ready(),
            championCatalog,
            TestItemCatalog.Ready(),
            new SaveCoachPackUseCase(resolvedStore));
        return new CoachingViewModel(
            resolvedLocalization,
            library,
            new PackTypeSelectionViewModel(),
            editor);
    }
}
