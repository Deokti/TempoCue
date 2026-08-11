using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Собирает граф зависимостей коучинга для тестов без дублирования composition root.</summary>
internal static class CoachingViewModelTestFactory
{
    public static CoachingViewModel Create(
        CoachPack builtInPack,
        LocalizationService? localization = null,
        TestCoachPackStore? store = null)
    {
        var resolvedStore = store ?? new TestCoachPackStore();
        var resolvedLocalization = localization ?? LocalizationService.Instance;
        var championCatalog = TestChampionCatalog.Ready();
        var library = new CoachingLibraryViewModel(
            [builtInPack],
            resolvedLocalization,
            championCatalog,
            new LoadCoachPackLibraryUseCase(resolvedStore),
            new SetCoachPackStateUseCase(resolvedStore),
            new DeleteCoachPackUseCase(resolvedStore));
        var itemCatalog = TestItemCatalog.Ready();
        var editor = new MatchupEditorViewModel(
            resolvedLocalization,
            TestGamePatchCatalog.Ready(),
            championCatalog,
            itemCatalog,
            new SaveCoachPackUseCase(resolvedStore));
        var details = new CoachingPackDetailsViewModel(
            resolvedLocalization,
            itemCatalog,
            new BuildLeagueItemSetExportUseCase(championCatalog, itemCatalog),
            new ExportCoachPackUseCase());
        return new CoachingViewModel(
            resolvedLocalization,
            library,
            details,
            new PackTypeSelectionViewModel(),
            editor);
    }
}
