using System.Windows.Controls;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

/// <summary>Проверяет завершение настройки оверлея при уходе со страницы.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class MainWindowOverlaySetupNavigationTests
{
    [Fact]
    public void NavigationFromSettings_ClosesActiveOverlaySetup()
    {
        WpfTestHost.Run(() =>
        {
            var setup = new TestOverlaySetupSession();
            using var context = new MainWindowTestContext(
                overlaySetupSession: setup);
            context.Show();

            WpfTestHost.Click(WpfTestHost.Find<Button>(context.Window, "SettingsNavigationButton"));
            var settings = WpfTestHost.Find<SettingsView>(context.Window, "SettingsContentPage");
            WpfTestHost.Click(WpfTestHost.Find<Button>(settings, "ConfigureOverlayButton"));
            WpfTestHost.Click(WpfTestHost.Find<Button>(context.Window, "HomeNavigationButton"));

            Assert.False(setup.IsActive);
            Assert.Equal(1, setup.CloseCount);
        });
    }
}
