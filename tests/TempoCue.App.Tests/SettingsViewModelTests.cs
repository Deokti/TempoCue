using TempoCue.App.Localization;
using TempoCue.App.ViewModels;
using TempoCue.App.Settings;
using TempoCue.Overlay;

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
        using var settingsContext = new TemporarySettingsContext();
        var settingsService = settingsContext.Service;

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
        }
    }

    [Fact]
    public void SaveOverlaySettings_PersistsDraftAndAppliesRuntimeState()
    {
        using var settingsContext = new TemporarySettingsContext();
        var service = settingsContext.Service;
        var runtime = new OverlaySettingsState(new OverlayUserSettings());
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            service,
            static () => { },
            runtime,
            null,
            static () => new OverlayPresentationSettings
            {
                HorizontalPosition = 0.25,
                VerticalPosition = 0.5
            });
        Assert.False(viewModel.IsOverlaySettingsDirty);

        viewModel.IsOverlayEnabled = false;
        viewModel.OverlayWidth = 400;
        viewModel.BackgroundOpacityPercent = 70;
        viewModel.IsFixedDuration = true;
        viewModel.FixedDurationSeconds = 12;
        viewModel.IsPersistentAdviceDisabled = true;
        Assert.True(viewModel.IsOverlaySettingsDirty);

        viewModel.OpenOverlaySetup();
        viewModel.SaveOverlaySettings();

        Assert.False(viewModel.IsOverlaySettingsDirty);
        Assert.Equal(service.Load().Overlay, runtime.Current);
        Assert.False(runtime.Current.IsEnabled);
        Assert.Equal(400, runtime.Current.Width);
        Assert.Equal(70, runtime.Current.BackgroundOpacityPercent);
        Assert.Equal(OverlayDurationMode.Fixed, runtime.Current.DurationMode);
        Assert.Equal(12, runtime.Current.FixedDurationSeconds);
        Assert.Equal(PersistentAdviceFrequency.Disabled, runtime.Current.PersistentAdviceFrequency);
        Assert.Equal(0.25, runtime.Current.HorizontalPosition);
        Assert.Equal(0.5, runtime.Current.VerticalPosition);
    }

    [Fact]
    public void ResetOverlaySettings_RestoresDefaultPlacementOnSave()
    {
        using var settingsContext = new TemporarySettingsContext();
        var runtime = new OverlaySettingsState(new OverlayUserSettings
        {
            HorizontalPosition = 0.2,
            VerticalPosition = 0.4
        });
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            settingsContext.Service,
            static () => { },
            runtime);

        viewModel.ResetOverlaySettings();
        viewModel.SaveOverlaySettings();

        Assert.Null(runtime.Current.HorizontalPosition);
        Assert.Null(runtime.Current.VerticalPosition);
        Assert.Equal(340, runtime.Current.Width);
        Assert.Equal(80, runtime.Current.BackgroundOpacityPercent);
    }
}
