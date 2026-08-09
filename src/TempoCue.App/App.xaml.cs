using System.IO;
using System.Windows;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
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
    private CurrentMatchViewModel? _currentMatchViewModel;
    private HomeViewModel? _homeViewModel;
    private SettingsViewModel? _settingsViewModel;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Язык выбирается до создания окна, чтобы пользователь не увидел
        // промежуточное переключение с языка по умолчанию.
        var settingsService = new AppSettingsService();
        var settings = settingsService.Load();
        LocalizationService.Instance.SetLanguage(settings.Language);

        var packPath = Path.Combine(
            AppContext.BaseDirectory,
            "Assets",
            "CoachPacks",
            "urgot-vs-sett.json");
        var pack = CoachPackLoader.LoadFromFile(packPath);
        var coach = new MatchupCoach(pack.Rules);

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

        MainWindow = new MainWindow(
            _currentMatchViewModel,
            _homeViewModel,
            _settingsViewModel);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _settingsViewModel?.Dispose();
        _homeViewModel?.Dispose();
        _currentMatchViewModel?.Dispose();
        _liveClientDataClient?.Dispose();
        base.OnExit(e);
    }
}
