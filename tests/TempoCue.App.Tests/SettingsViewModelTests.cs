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
    public void ExperimentalRecognizer_PersistsImmediatelyAndAsksForRestart()
    {
        using var settingsContext = new TemporarySettingsContext();
        var service = settingsContext.Service;
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            service,
            static () => { });

        Assert.Equal(MinimapRecognizerChoice.Default, viewModel.MinimapRecognizer);
        Assert.False(viewModel.HasExperimentalRecognizerRestartNotice);
        Assert.False(viewModel.HasUnsupportedChampionsNotice);

        viewModel.MinimapRecognizer = MinimapRecognizerChoice.OwnModel;

        // Выбор сохраняется сразу, но применяется только при следующем запуске,
        // поэтому пользователю показывается напоминание о перезапуске.
        Assert.Equal(MinimapRecognizerChoice.OwnModel, service.Load().MinimapRecognizer);
        Assert.True(viewModel.HasExperimentalRecognizerRestartNotice);
        // Предупреждение о неизвестных чемпионах относится только к чужой модели.
        Assert.False(viewModel.HasUnsupportedChampionsNotice);

        viewModel.MinimapRecognizer = MinimapRecognizerChoice.DeeperLeague;

        Assert.True(viewModel.HasUnsupportedChampionsNotice);

        viewModel.MinimapRecognizer = MinimapRecognizerChoice.Default;

        Assert.Equal(MinimapRecognizerChoice.Default, service.Load().MinimapRecognizer);
        Assert.False(viewModel.HasExperimentalRecognizerRestartNotice);
    }

    [Fact]
    public void RecognitionThreads_DefaultToAutomaticAndPersistWithRestartNotice()
    {
        using var settingsContext = new TemporarySettingsContext();
        var service = settingsContext.Service;
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            service,
            static () => { });

        Assert.Equal(
            MinimapRecognitionThreadOptionViewModel.AutomaticThreads,
            viewModel.MinimapRecognitionThreads);
        Assert.False(viewModel.HasRecognitionThreadsRestartNotice);

        var manualOption = viewModel.MinimapRecognitionThreadOptions
            .First(option =>
                option.Threads != MinimapRecognitionThreadOptionViewModel.AutomaticThreads);
        viewModel.MinimapRecognitionThreads = manualOption.Threads;

        Assert.Equal(manualOption.Threads, service.Load().MinimapRecognitionThreads);
        Assert.True(viewModel.HasRecognitionThreadsRestartNotice);
    }

    [Fact]
    public void RecognitionThreads_FallBackToAutomaticWhenMachineCannotOfferStoredValue()
    {
        using var settingsContext = new TemporarySettingsContext();
        var service = settingsContext.Service;
        // Настройки могли переехать с машины, где ядер было больше. Предлагать
        // отсутствующее значение нельзя: список строится по текущей машине.
        service.SaveMinimapRecognitionThreads(Environment.ProcessorCount * 4);

        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            service,
            static () => { });

        Assert.Equal(
            MinimapRecognitionThreadOptionViewModel.AutomaticThreads,
            viewModel.MinimapRecognitionThreads);
        Assert.False(viewModel.HasRecognitionThreadsRestartNotice);
    }

    [Fact]
    public void RecognitionThreadOptions_StayWithinTheProcessorsOfThisMachine()
    {
        using var settingsContext = new TemporarySettingsContext();
        using var viewModel = new SettingsViewModel(
            LocalizationService.Instance,
            settingsContext.Service,
            static () => { });

        var options = viewModel.MinimapRecognitionThreadOptions;

        Assert.Equal(
            MinimapRecognitionThreadOptionViewModel.AutomaticThreads,
            options[0].Threads);
        Assert.All(
            options.Skip(1),
            option => Assert.InRange(option.Threads, 1, Environment.ProcessorCount));
        Assert.Contains(options, option => option.Threads == Environment.ProcessorCount);
        Assert.Equal(options.Select(option => option.Threads).Distinct().Count(), options.Count);
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
