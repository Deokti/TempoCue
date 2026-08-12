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
        TestCoachPackStore? store = null,
        TestMapControlPackStore? mapControlStore = null)
    {
        var resolvedStore = store ?? new TestCoachPackStore();
        var resolvedLocalization = localization ?? LocalizationService.Instance;
        var resolvedMapControlStore = mapControlStore ?? new TestMapControlPackStore();
        var championCatalog = TestChampionCatalog.Ready();
        var library = new CoachingLibraryViewModel(
            [builtInPack],
            resolvedLocalization,
            championCatalog,
            new LoadCoachPackLibraryUseCase(resolvedStore),
            new SetCoachPackStateUseCase(resolvedStore),
            new DeleteCoachPackUseCase(resolvedStore),
            new LoadMapControlPackLibraryUseCase(resolvedMapControlStore),
            new SetMapControlPackStateUseCase(resolvedMapControlStore),
            new DeleteMapControlPackUseCase(resolvedMapControlStore));
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
        var import = new CoachPackImportDialogViewModel(
            resolvedLocalization,
            new ImportCoachPackUseCase(resolvedStore, [builtInPack.Id]));
        return new CoachingViewModel(
            resolvedLocalization,
            library,
            details,
            new MapControlPackDetailsViewModel(
                resolvedLocalization,
                new ExportMapControlPackUseCase()),
            import,
            new PackTypeSelectionViewModel(),
            editor,
            new MapControlEditorViewModel(
                resolvedLocalization,
                TestGamePatchCatalog.Ready(),
                new SaveMapControlPackUseCase(resolvedMapControlStore)));
    }
}
