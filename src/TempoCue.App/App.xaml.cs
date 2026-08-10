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

        // Встроенный набор остаётся источником правил матча до следующей итерации,
        // в которой движок будет выбирать включённый набор из локальной библиотеки.
        var packPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "CoachPacks",
            "urgot-vs-sett.json");
        var pack = CoachPackLoader.LoadFromFile(packPath);
        var coach = new MatchupCoach(pack.Rules);

        // Инфраструктура матча создаётся здесь, чтобы View и ViewModel не конструировали HTTP-клиенты.
        _liveClientDataClient = new LiveClientDataClient();
        var activeMatchSession = new ActiveMatchSession(
            _liveClientDataClient,
            coach,
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
        var coachPackDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TempoCue",
            "CoachPacks");
        var coachPackStore = new LocalCoachPackStore(coachPackDirectory);
        var saveCoachPack = new SaveCoachPackUseCase(coachPackStore);
        var loadCoachPackLibrary = new LoadCoachPackLibraryUseCase(coachPackStore);
        var setCoachPackState = new SetCoachPackStateUseCase(coachPackStore);
        _coachingViewModel = new CoachingViewModel(
            pack,
            LocalizationService.Instance,
            _gamePatchCatalog,
            _championCatalog,
            _itemCatalog,
            saveCoachPack,
            loadCoachPackLibrary,
            setCoachPackState);

        // Окно получает полностью собранный граф зависимостей и только связывает ViewModel с View.
        MainWindow = new MainWindow(
            _currentMatchViewModel,
            _homeViewModel,
            _settingsViewModel,
            _coachingViewModel);
        MainWindow.Show();
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
