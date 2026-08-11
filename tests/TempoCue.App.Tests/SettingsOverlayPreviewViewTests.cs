using System.IO;
using System.Windows.Controls;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

/// <summary>Проверяет доступ пользователя к предпросмотру через страницу настроек.</summary>
[Collection(LocalizationCollection.Name)]
public sealed class SettingsOverlayPreviewViewTests
{
    [Fact]
    public void PreviewButton_InvokesConfiguredOverlayAction()
    {
        var previewCount = 0;
        WpfTestHost.Run(() =>
        {
            using var context = new MainWindowTestContext(() => previewCount++);
            context.Show();
            WpfTestHost.Click(WpfTestHost.Find<Button>(context.Window, "SettingsNavigationButton"));
            var settings = WpfTestHost.Find<SettingsView>(context.Window, "SettingsContentPage");
            var previewButton = WpfTestHost.Find<Button>(settings, "ShowOverlayPreviewButton");

            Assert.Equal("Показать следующее уведомление", previewButton.Content);
            WpfTestHost.Click(previewButton);
            context.Localization.SetLanguage("en-US");
            WpfTestHost.Pump();
            Assert.Equal("Show next notification", previewButton.Content);
            WpfTestHost.Click(previewButton);
            WpfTestHost.Click(previewButton);

            Assert.Equal(3, previewCount);

            var snapshotDirectory = Environment.GetEnvironmentVariable("TEMPOCUE_UI_SNAPSHOT_DIR");
            if (!string.IsNullOrWhiteSpace(snapshotDirectory))
            {
                WpfTestHost.SavePng(settings, Path.Combine(snapshotDirectory, "settings-overlay-en.png"));
            }
        });
    }
}
