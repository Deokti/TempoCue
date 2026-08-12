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

    public AppSettingsService Service => new(Path.Combine(_directory, "settings.json"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
