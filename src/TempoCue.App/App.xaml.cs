using System.IO;
using System.Windows;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.App.Persistence;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Riot;

namespace TempoCue.App;

/// <summary>
/// Точка входа WPF-приложения.
/// </summary>
public partial class App : System.Windows.Application
{
    private LiveClientDataClient? _liveClientDataClient;
    private DataDragonClient? _dataDragonClient;
    private DataDragonGamePatchCatalog? _gamePatchCatalog;
    private DataDragonChampionCatalog? _championCatalog;
    private DataDragonItemCatalog? _itemCatalog;
    private CurrentMatchViewModel? _currentMatchViewModel;
    private HomeViewModel? _homeViewModel;
    private SettingsViewModel? _settingsViewModel;
    private CoachingViewModel? _coachingViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Язык выбирается до создания окна, чтобы пользователь не увидел
        // промежуточное переключение с языка по умолчанию.
        var settingsService = new AppSettingsService();
        var settings = settingsService.Load();
        LocalizationService.Instance.SetLanguage(settings.Language);

        // Каталог может быть пустым: встроенные наборы поставляются только после
        // отдельной проверки их содержания и актуальности.
        var builtInPacks = LoadBuiltInCoachPacks();

        var coachPackDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TempoCue",
            "CoachPacks");
        var coachPackStore = new LocalCoachPackStore(coachPackDirectory);
        var saveCoachPack = new SaveCoachPackUseCase(coachPackStore);
        var loadCoachPackLibrary = new LoadCoachPackLibraryUseCase(coachPackStore);
        var setCoachPackState = new SetCoachPackStateUseCase(coachPackStore);
        var deleteCoachPack = new DeleteCoachPackUseCase(coachPackStore);
        var importCoachPack = new ImportCoachPackUseCase(
            coachPackStore,
            builtInPacks.Select(static pack => pack.Id));
        var activeCoachPackSelector = new SelectActiveCoachPackUseCase(
            builtInPacks,
            loadCoachPackLibrary);

        // Инфраструктура матча создаётся здесь, чтобы View и ViewModel не конструировали HTTP-клиенты.
        _liveClientDataClient = new LiveClientDataClient();
        var activeMatchSession = new ActiveMatchSession(
            _liveClientDataClient,
            activeCoachPackSelector,
            TimeSpan.FromSeconds(1));
        _currentMatchViewModel = new CurrentMatchViewModel(
            activeMatchSession,
            LocalizationService.Instance);
        _homeViewModel = new HomeViewModel(
            _currentMatchViewModel,
            LocalizationService.Instance);
        _settingsViewModel = new SettingsViewModel(
            LocalizationService.Instance,
            settingsService);
        // Каталоги Data Dragon используют общий локальный кэш патчей и чемпионов.
        var dataDragonCacheDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TempoCue",
            "Cache",
            "DataDragon");
        _dataDragonClient = new DataDragonClient();
        _gamePatchCatalog = new DataDragonGamePatchCatalog(
            _dataDragonClient,
            dataDragonCacheDirectory);
        _championCatalog = new DataDragonChampionCatalog(
            _dataDragonClient,
            dataDragonCacheDirectory);
        _itemCatalog = new DataDragonItemCatalog(
            _dataDragonClient,
            dataDragonCacheDirectory);
        var coachingLibraryViewModel = new CoachingLibraryViewModel(
            builtInPacks,
            LocalizationService.Instance,
            _championCatalog,
            loadCoachPackLibrary,
            setCoachPackState,
            deleteCoachPack);
        coachingLibraryViewModel.LibraryChanged += (_, _) =>
            activeCoachPackSelector.Invalidate();
        var buildLeagueItemSetExport = new BuildLeagueItemSetExportUseCase(
            _championCatalog,
            _itemCatalog);
        var coachingPackDetailsViewModel = new CoachingPackDetailsViewModel(
            LocalizationService.Instance,
            _itemCatalog,
            buildLeagueItemSetExport,
            new ExportCoachPackUseCase());
        var matchupEditorViewModel = new MatchupEditorViewModel(
            LocalizationService.Instance,
            _gamePatchCatalog,
            _championCatalog,
            _itemCatalog,
            saveCoachPack);
        _coachingViewModel = new CoachingViewModel(
            LocalizationService.Instance,
            coachingLibraryViewModel,
            coachingPackDetailsViewModel,
            new CoachPackImportDialogViewModel(
                LocalizationService.Instance,
                importCoachPack),
            new PackTypeSelectionViewModel(),
            matchupEditorViewModel);

        // Окно получает полностью собранный граф зависимостей и только связывает ViewModel с View.
        MainWindow = new MainWindow(
            _currentMatchViewModel,
            _homeViewModel,
            _settingsViewModel,
            _coachingViewModel);
        MainWindow.Show();
    }

    private static IReadOnlyList<CoachPack> LoadBuiltInCoachPacks()
    {
        var directory = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "CoachPacks");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var packs = new List<CoachPack>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json")
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(path);
            packs.Add(CoachPackLoader.Load(stream));
        }

        return packs;
    }

    // Освобождение выполняется в обратном порядке владения долгоживущими объектами.
    protected override void OnExit(ExitEventArgs e)
    {
        _coachingViewModel?.Dispose();
        _settingsViewModel?.Dispose();
        _homeViewModel?.Dispose();
        _currentMatchViewModel?.Dispose();
        _dataDragonClient?.Dispose();
        _liveClientDataClient?.Dispose();
        base.OnExit(e);
    }
}
