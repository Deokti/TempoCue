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
        var setup = new TestOverlaySetupSession();
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            service,
            static () => { },
            runtime,
            setup);
        Assert.False(viewModel.IsOverlaySettingsDirty);

        viewModel.IsOverlayEnabled = false;
        viewModel.OverlayWidth = 400;
        viewModel.BackgroundOpacityPercent = 70;
        viewModel.IsFixedDuration = true;
        viewModel.FixedDurationSeconds = 12;
        viewModel.IsPersistentAdviceDisabled = true;
        Assert.True(viewModel.IsOverlaySettingsDirty);

        viewModel.OpenOverlaySetup();
        setup.MoveTo(0.25, 0.5);
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
        Assert.Equal(PersistentAdviceFrequency.EveryLevel, runtime.Current.PersistentAdviceFrequency);
    }

    [Fact]
    public void ActiveOverlaySetup_AppliesDraftAndStopsAfterClosing()
    {
        using var settingsContext = new TemporarySettingsContext();
        var setup = new TestOverlaySetupSession();
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            settingsContext.Service,
            static () => { },
            overlaySetupSession: setup);

        viewModel.OpenOverlaySetup();
        setup.MoveTo(0.25, 0.5);
        viewModel.OverlayWidth = 400;
        viewModel.BackgroundOpacityPercent = 70;

        Assert.Single(setup.OpenedSettings);
        Assert.Equal(2, setup.AppliedSettings.Count);
        var liveDraft = setup.AppliedSettings[^1];
        Assert.Equal(400, liveDraft.Width);
        Assert.Equal(70, liveDraft.BackgroundOpacityPercent);
        Assert.Equal(0.25, liveDraft.HorizontalPosition);
        Assert.Equal(0.5, liveDraft.VerticalPosition);

        viewModel.CloseOverlaySetup();
        viewModel.OverlayWidth = 340;

        Assert.Equal(1, setup.CloseCount);
        Assert.Equal(2, setup.AppliedSettings.Count);
    }

    [Fact]
    public void ResetOverlaySettings_UpdatesActiveSetupWithDefaultPlacement()
    {
        using var settingsContext = new TemporarySettingsContext();
        var setup = new TestOverlaySetupSession();
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            settingsContext.Service,
            static () => { },
            overlaySetupSession: setup);

        viewModel.OpenOverlaySetup();
        setup.MoveTo(0.25, 0.5);
        viewModel.ResetOverlaySettings();

        var defaults = Assert.Single(setup.AppliedSettings);
        Assert.Equal(340, defaults.Width);
        Assert.Equal(80, defaults.BackgroundOpacityPercent);
        Assert.Null(defaults.HorizontalPosition);
        Assert.Null(defaults.VerticalPosition);
    }
}
