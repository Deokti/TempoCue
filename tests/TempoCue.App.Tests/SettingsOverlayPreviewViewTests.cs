using System.IO;
using System.Windows;
using System.Windows.Controls;
using TempoCue.App.Controls;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

/// <summary>Проверяет интерактивные состояния настроек и доступ к настройке оверлея.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class SettingsOverlayPreviewViewTests
{
    [Fact]
    public void OverlayControls_SwitchModesAndInvokeSetupAction()
    {
        var previewCount = 0;
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext(() => previewCount++);
            context.Show();
            WpfTestHost.Click(WpfTestHost.Find<Button>(context.Window, "SettingsNavigationButton"));
            var settings = WpfTestHost.Find<SettingsView>(context.Window, "SettingsContentPage");
            var enabledSwitch = WpfTestHost.Find<System.Windows.Controls.Primitives.ToggleButton>(
                settings,
                "OverlayEnabledSwitch");
            var configureButton = WpfTestHost.Find<Button>(settings, "ConfigureOverlayButton");
            var scrollViewer = WpfTestHost.Find<ScrollViewer>(settings, "SettingsScrollViewer");
            var automaticChoice = WpfTestHost.Find<TempoChoiceCard>(settings, "AutomaticDurationChoice");
            var fixedChoice = WpfTestHost.Find<TempoChoiceCard>(settings, "FixedDurationChoice");
            var fixedPanel = WpfTestHost.Find<Border>(settings, "FixedDurationPanel");
            var persistentChoice = WpfTestHost.Find<TempoChoiceCard>(settings, "PersistentOnceChoice");

            Assert.True(enabledSwitch.IsChecked);
            Assert.True(configureButton.IsEnabled);
            Assert.True(automaticChoice.IsChecked);
            Assert.Equal(Visibility.Collapsed, fixedPanel.Visibility);
            Assert.True(persistentChoice.IsChecked);
            Assert.Equal("рекомендуется", persistentChoice.Notice);

            var snapshotDirectory = Environment.GetEnvironmentVariable("TEMPOCUE_UI_SNAPSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(snapshotDirectory))
            {
                WpfTestHost.SavePng(settings, Path.Combine(snapshotDirectory, "settings-overlay-ru.png"));
            }

            WpfTestHost.Click(fixedChoice);
            Assert.True(fixedChoice.IsChecked);
            Assert.Equal(Visibility.Visible, fixedPanel.Visibility);

            context.Localization.SetLanguage("en-US");
            WpfTestHost.Pump();
            Assert.Equal("Configure overlay", configureButton.Content);
            Assert.Equal("recommended", persistentChoice.Notice);

            WpfTestHost.Click(enabledSwitch);
            Assert.False(enabledSwitch.IsChecked);
            Assert.True(configureButton.IsEnabled);
            WpfTestHost.Click(configureButton);
            Assert.Equal(1, previewCount);

            if (!string.IsNullOrWhiteSpace(snapshotDirectory))
            {
                scrollViewer.ScrollToEnd();
                WpfTestHost.Pump();
                WpfTestHost.SavePng(settings, Path.Combine(snapshotDirectory, "settings-overlay-en-bottom.png"));
            }
        });
    }
}
