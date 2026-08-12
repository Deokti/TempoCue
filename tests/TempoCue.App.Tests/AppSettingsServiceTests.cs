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
}
