using TempoCue.App.Localization;
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
        using var settingsContext = new TemporarySettingsContext();
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            settingsContext.Service,
            () => previewCount++);

        viewModel.ShowOverlayPreview();

        Assert.Equal(1, previewCount);
    }
}
