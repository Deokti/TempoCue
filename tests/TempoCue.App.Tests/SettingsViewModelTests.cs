using System.IO;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет применение и сохранение настроек без создания окна.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class SettingsViewModelTests
{
    [Fact]
    public void SelectedLanguageCode_ChangesInterfaceLanguageAndPersistsSelection()
    {
        var localization = LocalizationService.Instance;
        var initialLanguage = localization.CurrentLanguageCode;
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            "TempoCue.Tests",
            Guid.NewGuid().ToString("N"));
        var settingsService = new AppSettingsService(
            Path.Combine(testDirectory, "settings.json"));

        try
        {
            localization.SetLanguage("ru-RU");
            using var viewModel = new SettingsViewModel(
                localization,
                settingsService,
                static () => { });

            viewModel.SelectedLanguageCode = "en-US";

            Assert.Equal("en-US", localization.CurrentLanguageCode);
            Assert.Equal("en-US", settingsService.Load().Language);
        }
        finally
        {
            localization.SetLanguage(initialLanguage);
            if (Directory.Exists(testDirectory))
            {
                Directory.Delete(testDirectory, recursive: true);
            }
        }
    }
}
