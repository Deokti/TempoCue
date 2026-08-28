using System.IO;
using TempoCue.App.Settings;
using TempoCue.Overlay;

namespace TempoCue.App.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public void SavingLanguageAndOverlay_PreservesBothGroups()
    {
        using var context = new TemporarySettingsContext();
        var service = context.Service;

        service.SaveOverlay(new OverlayUserSettings
        {
            IsEnabled = false,
            Width = 400,
            BackgroundOpacityPercent = 65,
            DurationMode = OverlayDurationMode.Fixed,
            FixedDurationSeconds = 14,
            PersistentAdviceFrequency = PersistentAdviceFrequency.Disabled
        });
        service.SaveLanguage("en-US");

        var loaded = service.Load();
        Assert.Equal("en-US", loaded.Language);
        Assert.False(loaded.Overlay.IsEnabled);
        Assert.Equal(400, loaded.Overlay.Width);
        Assert.Equal(65, loaded.Overlay.BackgroundOpacityPercent);
        Assert.Equal(14, loaded.Overlay.FixedDurationSeconds);
        Assert.Equal(PersistentAdviceFrequency.Disabled, loaded.Overlay.PersistentAdviceFrequency);
    }

    [Fact]
    public void SavingRecognitionThreads_KeepsLanguageAndSurvivesReload()
    {
        using var context = new TemporarySettingsContext();
        var service = context.Service;
        service.SaveLanguage("en-US");

        service.SaveMinimapRecognitionThreads(4);

        var loaded = service.Load();
        Assert.Equal(4, loaded.MinimapRecognitionThreads);
        Assert.Equal("en-US", loaded.Language);
    }

    [Fact]
    public void LoadingRecognitionThreads_TreatsNegativeValueAsAutomatic()
    {
        using var context = new TemporarySettingsContext();
        // Файл настроек правится вручную, и отрицательное число потоков не должно
        // ронять запуск: оно означает «выбрать по машине».
        File.WriteAllText(
            context.SettingsPath,
            """{"language":"ru-RU","minimapRecognitionThreads":-8}""");

        Assert.Equal(0, context.Service.Load().MinimapRecognitionThreads);
    }

    [Fact]
    public void MinimapRecognizer_DefaultsToOwnModelWhenNobodyChose()
    {
        using var context = new TemporarySettingsContext();

        var settings = context.Service.Load();

        // Выбора нет — применяется умолчание приложения.
        Assert.Null(settings.MinimapRecognizer);
        Assert.Equal(
            MinimapRecognizerChoice.OwnModel,
            settings.EffectiveMinimapRecognizer);
    }

    [Fact]
    public void MinimapRecognizer_ForgetsTheChoiceOfARemovedRecognizer()
    {
        using var context = new TemporarySettingsContext();
        File.WriteAllText(
            context.SettingsPath,
            """{ "language": "en-US", "minimapRecognizer": 2 }""");

        var settings = context.Service.Load();

        // Двойка — удалённая сторонняя модель DeeperLeague. Выбор исчезнувшего
        // варианта читается как «выбора нет»; остальные настройки при этом целы,
        // а не сброшены целиком.
        Assert.Null(settings.MinimapRecognizer);
        Assert.Equal(
            AppSettings.DefaultMinimapRecognizer,
            settings.EffectiveMinimapRecognizer);
        Assert.Equal("en-US", settings.Language);
    }

    [Fact]
    public void MinimapRecognizer_IgnoresTheOldExperimentalCheckbox()
    {
        using var context = new TemporarySettingsContext();
        File.WriteAllText(
            context.SettingsPath,
            """{ "useExperimentalMinimapRecognizer": true }""");

        var settings = context.Service.Load();

        // Галочка означала стороннюю модель, которой больше нет. Ничего
        // осмысленного она теперь не выражает — применяется умолчание.
        Assert.Equal(
            AppSettings.DefaultMinimapRecognizer,
            settings.EffectiveMinimapRecognizer);
    }

    [Fact]
    public void MinimapRecognizer_KeepsDeliberateChoiceOfTheBuiltInPipeline()
    {
        using var context = new TemporarySettingsContext();
        context.Service.SaveMinimapRecognizer(MinimapRecognizerChoice.Default);

        var settings = context.Service.Load();

        // Штатный конвейер, выбранный намеренно, — это выбор, а не отсутствие
        // выбора. Умолчание его не перебивает.
        Assert.Equal(MinimapRecognizerChoice.Default, settings.MinimapRecognizer);
        Assert.Equal(
            MinimapRecognizerChoice.Default,
            settings.EffectiveMinimapRecognizer);
    }
}
