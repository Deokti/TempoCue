using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TempoCue.App.ViewModels;
using TempoCue.App.Views;

namespace TempoCue.App.Tests;

/// <summary>Проверяет стабильную геометрию динамического статуса видимости.</summary>
public sealed class SettingsParticipantStatusTemplateTests
{
    [Fact]
    public void ParticipantStatus_DoesNotChangeHeightWhenRecognitionAppears()
    {
        WpfTestHost.Run(() =>
        {
            var participantDiagnosticsView = new VisionParticipantDiagnosticsView();
            var template = Assert.IsType<DataTemplate>(
                participantDiagnosticsView.Resources["VisionParticipantStatusTemplate"]);
            var presenter = new ContentPresenter
            {
                Width = 340,
                VerticalAlignment = VerticalAlignment.Top,
                ContentTemplate = template,
                Content = CreateStatus(
                    "Riot: CHAOS · ожидается: союзник · диагностический контур: —",
                    "Не виден")
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
                    "Riot: CHAOS · ожидается: союзник · диагностический контур: союзник",
                    "Виден · уверенность 86 %");
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
