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

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Язык выбирается до создания окна, чтобы пользователь не увидел
        // промежуточное переключение с языка по умолчанию.
        var settings = AppSettingsService.Load();
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

        MainWindow = new MainWindow(_currentMatchViewModel);
        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _currentMatchViewModel?.Stop();
        _liveClientDataClient?.Dispose();
        base.OnExit(e);
    }
}
