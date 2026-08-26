using System.Diagnostics;
using System.IO;
using System.Windows;
using TempoCue.App.Coordinators;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.App.Persistence;
using TempoCue.App.ViewModels;
using TempoCue.Application;
using TempoCue.Core;
using TempoCue.Overlay;
using TempoCue.Riot;
using TempoCue.Vision;
using TempoCue.Vision.DeeperLeague;
using TempoCue.Vision.OwnModel;

namespace TempoCue.App;

/// <summary>
/// Точка входа WPF-приложения.
/// </summary>
public partial class App : System.Windows.Application
{
    private LiveClientDataClient? _liveClientDataClient;
    private LeagueClientChampionSelectClient? _championSelectClient;
    private DataDragonClient? _dataDragonClient;
    private DataDragonGamePatchCatalog? _gamePatchCatalog;
    private DataDragonChampionCatalog? _championCatalog;
    private DataDragonItemCatalog? _itemCatalog;
    private ActiveMatchMonitor? _activeMatchMonitor;
    private CurrentMatchViewModel? _currentMatchViewModel;
    private HomeViewModel? _homeViewModel;
    private SettingsViewModel? _settingsViewModel;
    private CoachingViewModel? _coachingViewModel;
    private IOverlayPresenter? _overlayPresenter;
    private OverlaySetupSession? _overlaySetupSession;
    private ActiveMatchOverlayCoordinator? _activeMatchOverlayCoordinator;
    private ActiveMatchMapAwarenessCoordinator? _activeMatchMapAwarenessCoordinator;
    private ActiveMatchVisionCoordinator? _activeMatchVisionCoordinator;
    private ChampionPortraitReferenceLoader? _championPortraitReferenceLoader;
    private WindowsGraphicsCaptureSession? _windowCaptureSession;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private int _overlayPreviewStep;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Язык выбирается до создания окна, чтобы пользователь не увидел
        // промежуточное переключение с языка по умолчанию.
        var settingsService = new AppSettingsService();
        var settings = settingsService.Load();
        LocalizationService.Instance.SetLanguage(settings.Language);
        var overlaySettings = new OverlaySettingsState(settings.Overlay);

        // Каталог может быть пустым: встроенные наборы поставляются только после
        // отдельной проверки их содержания и актуальности.
        var builtInPacks = LoadBuiltInCoachPacks();
        var builtInMapControlPacks = LoadBuiltInMapControlPacks();

        var coachPackDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TempoCue",
            "CoachPacks");
        var coachPackStore = new LocalCoachPackStore(coachPackDirectory);
        var saveCoachPack = new SaveCoachPackUseCase(coachPackStore);
        var mapControlPackDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TempoCue",
            "MapControlPacks");
        var mapControlPackStore = new LocalMapControlPackStore(mapControlPackDirectory);
        var saveMapControlPack = new SaveMapControlPackUseCase(mapControlPackStore);
        var loadMapControlPackLibrary = new LoadMapControlPackLibraryUseCase(mapControlPackStore);
        var setMapControlPackState = new SetMapControlPackStateUseCase(mapControlPackStore);
        var deleteMapControlPack = new DeleteMapControlPackUseCase(mapControlPackStore);
        var loadCoachPackLibrary = new LoadCoachPackLibraryUseCase(coachPackStore);
        var setCoachPackState = new SetCoachPackStateUseCase(coachPackStore);
        var deleteCoachPack = new DeleteCoachPackUseCase(coachPackStore);
        var importCoachPack = new ImportCoachPackUseCase(
            coachPackStore,
            builtInPacks.Select(static pack => pack.Id));
        var importMapControlPack = new ImportMapControlPackUseCase(
            mapControlPackStore,
            builtInMapControlPacks.Select(static pack => pack.Id));
        var activeCoachPackSelector = new SelectActiveCoachPackUseCase(
            builtInPacks,
            loadCoachPackLibrary);
        var activeMapControlPackSelector = new SelectActiveMapControlPacksUseCase(
            loadMapControlPackLibrary,
            builtInMapControlPacks);

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

        // Инфраструктура матча создаётся здесь, чтобы View и ViewModel не конструировали HTTP-клиенты.
        _liveClientDataClient = new LiveClientDataClient();
        _championSelectClient = new LeagueClientChampionSelectClient();
        var leagueClientRegionSource = new LeagueClientRegionSource();
        var activeMatchSession = new ActiveMatchSession(
            _liveClientDataClient,
            activeCoachPackSelector,
            TimeSpan.FromSeconds(1),
            activeMapControlPackSelector,
            _championSelectClient);
        _activeMatchMonitor = new ActiveMatchMonitor(activeMatchSession);
        var currentMatchViewModel = new CurrentMatchViewModel(
            _activeMatchMonitor,
            _championCatalog,
            leagueClientRegionSource,
            LocalizationService.Instance);
        _currentMatchViewModel = currentMatchViewModel;
        var homeViewModel = new HomeViewModel(
            currentMatchViewModel,
            LocalizationService.Instance);
        _homeViewModel = homeViewModel;
        _overlayPresenter = new OverlayHost(Dispatcher);
        _overlayPresenter.ApplySettings(overlaySettings.Current.ToPresentationSettings());
        overlaySettings.Changed += (_, _) =>
            _overlayPresenter?.ApplySettings(overlaySettings.Current.ToPresentationSettings());
        _activeMatchOverlayCoordinator = new ActiveMatchOverlayCoordinator(
            _activeMatchMonitor,
            _overlayPresenter,
            LocalizationService.Instance,
            overlaySettings);
        _overlaySetupSession = new OverlaySetupSession(
            _overlayPresenter,
            overlaySettings,
            LocalizationService.Instance);
        _windowCaptureSession = new WindowsGraphicsCaptureSession(
            CreateFrameAnalyzer(settings));
        _championPortraitReferenceLoader = new ChampionPortraitReferenceLoader();
        _activeMatchVisionCoordinator = new ActiveMatchVisionCoordinator(
            _activeMatchMonitor,
            _championCatalog,
            _championPortraitReferenceLoader,
            _windowCaptureSession,
            LocalizationService.Instance,
            _windowCaptureSession);
        _activeMatchMapAwarenessCoordinator = new ActiveMatchMapAwarenessCoordinator(
            _activeMatchMonitor,
            _activeMatchVisionCoordinator,
            _overlayPresenter,
            LocalizationService.Instance,
            overlaySettings);
        var captureDiagnostics = new CaptureDiagnosticsViewModel(
            _windowCaptureSession,
            new WindowsGraphicsCaptureSupport().IsSupported,
            LocalizationService.Instance,
            _activeMatchVisionCoordinator);
        _settingsViewModel = new SettingsViewModel(
            LocalizationService.Instance,
            settingsService,
            ShowOverlayPreview,
            overlaySettings,
            _overlaySetupSession,
            captureDiagnostics,
            new ApplicationRestarter());
        var coachingLibraryViewModel = new CoachingLibraryViewModel(
            builtInPacks,
            LocalizationService.Instance,
            _championCatalog,
            loadCoachPackLibrary,
            setCoachPackState,
            deleteCoachPack,
            loadMapControlPackLibrary,
            setMapControlPackState,
            deleteMapControlPack,
            builtInMapControlPacks);
        coachingLibraryViewModel.LibraryChanged += (_, _) =>
        {
            activeCoachPackSelector.Invalidate();
            activeMapControlPackSelector.Invalidate();
            homeViewModel.SetCoachPackCount(coachingLibraryViewModel.TotalPackCount);
        };
        var buildLeagueItemSetExport = new BuildLeagueItemSetExportUseCase(
            _championCatalog,
            _itemCatalog);
        var coachingPackDetailsViewModel = new CoachingPackDetailsViewModel(
            LocalizationService.Instance,
            _itemCatalog,
            buildLeagueItemSetExport,
            new ExportCoachPackUseCase());
        var mapControlPackDetailsViewModel = new MapControlPackDetailsViewModel(
            LocalizationService.Instance,
            new ExportMapControlPackUseCase());
        var matchupEditorViewModel = new MatchupEditorViewModel(
            LocalizationService.Instance,
            _gamePatchCatalog,
            _championCatalog,
            _itemCatalog,
            saveCoachPack);
        var mapControlEditorViewModel = new MapControlEditorViewModel(
            LocalizationService.Instance,
            _gamePatchCatalog,
            saveMapControlPack);
        _coachingViewModel = new CoachingViewModel(
            LocalizationService.Instance,
            coachingLibraryViewModel,
            coachingPackDetailsViewModel,
            mapControlPackDetailsViewModel,
            new CoachPackImportDialogViewModel(
                LocalizationService.Instance,
                new ImportPortableCoachPackUseCase(
                    importCoachPack,
                    importMapControlPack)),
            new PackTypeSelectionViewModel(),
            matchupEditorViewModel,
            mapControlEditorViewModel);

        // Окно получает полностью собранный граф зависимостей и только связывает ViewModel с View.
        MainWindow = new MainWindow(
            _currentMatchViewModel,
            _homeViewModel,
            _settingsViewModel,
            _coachingViewModel);
        MainWindow.Show();
        // Монитор матча запускается раньше захвата: захват спрашивает у него,
        // началась ли игра, и до первого опроса ответ был бы «нет» дольше нужного.
        _activeMatchMonitor.Start();
        _windowCaptureSession.EnableAutomaticLeagueWindowCapture(
            IsMatchRunning,
            _lifetimeCancellation.Token);
        _ = InitializeScreenDataAsync(
            coachingLibraryViewModel,
            currentMatchViewModel,
            _lifetimeCancellation.Token);
    }

    private static async Task InitializeScreenDataAsync(
        CoachingLibraryViewModel coachingLibrary,
        CurrentMatchViewModel currentMatch,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(
                coachingLibrary.EnsureLoadedAsync(cancellationToken),
                currentMatch.LoadChampionImagesAsync(cancellationToken),
                currentMatch.LoadPlayerProfileRegionAsync(cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            // Ошибка фоновой подготовки не должна закрывать окно: каждый экран сохраняет
            // безопасное состояние без каталога, а причина остаётся доступна в диагностике.
            Trace.TraceError($"Не удалось подготовить данные экранов: {exception}");
        }
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

    private static IReadOnlyList<MapControlPack> LoadBuiltInMapControlPacks()
    {
        var directory = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "MapControlPacks");
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var packs = new List<MapControlPack>();
        foreach (var path in Directory.EnumerateFiles(directory, "*.json")
                     .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
        {
            using var stream = File.OpenRead(path);
            packs.Add(MapControlPackLoader.Load(stream));
        }

        return packs;
    }

    private void ShowOverlayPreview()
    {
        if (_overlayPresenter is null)
        {
            return;
        }

        var localization = LocalizationService.Instance;
        _overlayPresenter.SetInteractionMode(OverlayInteractionMode.Setup);
        var step = _overlayPreviewStep;
        var notification = step switch
        {
            0 => CreateOverlayPreviewNotification(
                localization,
                "Warning",
                OverlayNotificationTone.Caution),
            1 => CreateOverlayPreviewNotification(
                localization,
                "Information",
                OverlayNotificationTone.Information),
            _ => CreateOverlayPreviewNotification(
                localization,
                "Danger",
                OverlayNotificationTone.Danger)
        };

        if (step == 0)
        {
            _overlayPresenter.ShowPreview([notification]);
        }
        else
        {
            _overlayPresenter.Show(notification);
        }

        _overlayPreviewStep = (step + 1) % 3;
    }

    private static OverlayNotification CreateOverlayPreviewNotification(
        LocalizationService localization,
        string resourceSuffix,
        OverlayNotificationTone tone) =>
        OverlayPreviewNotificationFactory.Create(localization, resourceSuffix, tone);

    /// <summary>
    /// Создаёт экспериментальный распознаватель, если он включён в настройках и его
    /// файлы лежат рядом с приложением. Отсутствие файлов не является ошибкой:
    /// сборка без них просто работает на штатном конвейере.
    /// </summary>
    /// <summary>
    /// Идёт ли матч. Ответ Live Client Data приходит, когда игровой мир загружен,
    /// то есть примерно тогда же, когда на экране появляется миникарта. Окно игры
    /// для этого не годится: оно существует с начала загрузки.
    /// </summary>
    private bool IsMatchRunning() =>
        _activeMatchMonitor?.CurrentUpdate?.Status is MatchSnapshotReadStatus.Success;

    private static IMinimapFrameAnalyzer? CreateFrameAnalyzer(AppSettings settings)
    {
        // Ноль означает «выбрать по машине», и такой выбор оба распознавателя
        // делают сами. Число потоков задаётся обоим: у модели один вызов на кадр,
        // но ONNX Runtime разворачивает внутри него собственный пул по числу ядер.
        var threads = settings.MinimapRecognitionThreads <= 0
            ? (int?)null
            : settings.MinimapRecognitionThreads;
        if (settings.MinimapRecognizer is MinimapRecognizerChoice.Default)
        {
            return threads is null
                ? null
                : new DefaultMinimapFrameAnalyzer(threads.Value);
        }

        var modelDirectory = settings.MinimapRecognizer switch
        {
            MinimapRecognizerChoice.OwnModel => OwnModelFrameAnalyzer.FindModelDirectory(),
            _ => DeeperLeagueFrameAnalyzer.FindModelDirectory()
        };
        if (modelDirectory is null)
        {
            Debug.WriteLine(
                $"Выбран распознаватель {settings.MinimapRecognizer}, но файлы модели не найдены.");
            return null;
        }

        try
        {
            return settings.MinimapRecognizer switch
            {
                MinimapRecognizerChoice.OwnModel =>
                    new OwnModelFrameAnalyzer(modelDirectory, threads),
                _ => new DeeperLeagueFrameAnalyzer(modelDirectory, threads)
            };
        }
        catch (Exception exception)
        {
            // Неудачная загрузка модели не должна лишать пользователя распознавания.
            Debug.WriteLine($"Не удалось загрузить модель: {exception}");
            return null;
        }
    }

    // Освобождение выполняется в обратном порядке владения долгоживущими объектами.
    protected override void OnExit(ExitEventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _activeMatchMapAwarenessCoordinator?.Dispose();
        _activeMatchVisionCoordinator?.Dispose();
        _championPortraitReferenceLoader?.Dispose();
        _activeMatchOverlayCoordinator?.Dispose();
        _overlaySetupSession?.Dispose();
        _overlayPresenter?.Dispose();
        _coachingViewModel?.Dispose();
        _settingsViewModel?.Dispose();
        DisposeWindowCaptureSession();
        _homeViewModel?.Dispose();
        _currentMatchViewModel?.Dispose();
        _activeMatchMonitor?.Dispose();
        _championSelectClient?.Dispose();
        _dataDragonClient?.Dispose();
        _liveClientDataClient?.Dispose();
        base.OnExit(e);
    }

    private void DisposeWindowCaptureSession()
    {
        var session = Interlocked.Exchange(ref _windowCaptureSession, null);
        if (session is null)
        {
            return;
        }

        try
        {
            // WPF завершает Application синхронно; сессия не возвращается в UI-контекст.
            session.DisposeAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Debug.WriteLine($"Не удалось штатно завершить захват окна: {exception}");
        }
    }
}
