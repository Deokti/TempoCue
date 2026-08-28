using System.IO;
using TempoCue.App.Settings;

namespace TempoCue.App.Tests;

/// <summary>Изолирует файл настроек теста и гарантирует удаление временных данных.</summary>
internal sealed class TemporarySettingsContext : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "TempoCue.Tests",
        Guid.NewGuid().ToString("N"));

    public TemporarySettingsContext() => Directory.CreateDirectory(_directory);

    /// <summary>Путь файла настроек: тесты повреждённых значений пишут его напрямую.</summary>
    public string SettingsPath => Path.Combine(_directory, "settings.json");

    public AppSettingsService Service => new(SettingsPath);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
