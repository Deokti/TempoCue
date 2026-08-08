using System.Windows;
using TempoCue.App.Localization;
using TempoCue.App.Settings;

namespace TempoCue.App;

/// <summary>
/// Точка входа WPF-приложения.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Язык выбирается до создания окна, чтобы пользователь не увидел
        // промежуточное переключение с языка по умолчанию.
        var settings = AppSettingsService.Load();
        LocalizationService.Instance.SetLanguage(settings.Language);

        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
