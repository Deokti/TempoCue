using System.IO;
using TempoCue.App.Localization;
using TempoCue.App.Settings;
using TempoCue.App.ViewModels;

namespace TempoCue.App.Tests;

/// <summary>Проверяет явный пользовательский запуск предпросмотра без окна и матча.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class SettingsOverlayPreviewTests
{
    [Fact]
    public void ShowOverlayPreview_InvokesInjectedUiAction()
    {
        var previewCount = 0;
        var settingsPath = Path.Combine(
            Path.GetTempPath(),
            "TempoCue.Tests",
            Guid.NewGuid().ToString("N"),
            "settings.json");
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            new AppSettingsService(settingsPath),
            () => previewCount++);

        viewModel.ShowOverlayPreview();

        Assert.Equal(1, previewCount);
    }
}
