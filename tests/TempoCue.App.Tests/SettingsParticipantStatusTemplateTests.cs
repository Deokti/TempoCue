using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

/// <summary>Проверяет стабильную геометрию динамического статуса распознавания.</summary>
public sealed class SettingsParticipantStatusTemplateTests
{
    [Fact]
    public void ParticipantStatus_DoesNotChangeHeightWhenRecognitionAppears()
    {
        WpfTestHost.Run(() =>
        {
            var settingsView = new SettingsView();
            var template = Assert.IsType<DataTemplate>(
                settingsView.Resources["VisionParticipantStatusTemplate"]);
            var presenter = new ContentPresenter
            {
                Width = 340,
                VerticalAlignment = VerticalAlignment.Top,
                ContentTemplate = template,
                Content = CreateStatus(
                    "Riot: CHAOS · ожидается: союзник · распознанный контур: —",
                    "Не распознан в текущем кадре")
            };
            var window = new Window
            {
                Content = presenter,
                Width = 360,
                Height = 180,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None
            };

            try
            {
                window.Show();
                WpfTestHost.Pump();
                presenter.UpdateLayout();
                var missingHeight = presenter.ActualHeight;

                presenter.Content = CreateStatus(
                    "Riot: CHAOS · ожидается: союзник · распознанный контур: союзник",
                    "Распознан сейчас · уверенность 86 %");
                WpfTestHost.Pump();
                presenter.UpdateLayout();

                Assert.Equal(missingHeight, presenter.ActualHeight, precision: 3);
                Assert.True(presenter.ActualHeight >= 106);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static VisionParticipantStatusViewModel CreateStatus(
        string teamTrace,
        string status) =>
        new("Gragas", "JGL", status, teamTrace, Brushes.SpringGreen);
}
