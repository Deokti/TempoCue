using System.Windows;
using TempoCue.Application;

namespace TempoCue.App;

/// <summary>
/// Связывает переходную главную страницу с общей ViewModel активного матча.
/// Сам экран матча использует WPF binding напрямую.
/// </summary>
public partial class MainWindow
{
    private async void HomeCheckButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _currentMatchViewModel.RefreshAsync();
        }
        catch (OperationCanceledException)
        {
            // Закрытие приложения может совпасть с ручной проверкой.
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _currentMatchViewModel.StateUpdated -= CurrentMatchViewModel_StateUpdated;
        _currentMatchViewModel.Dispose();
    }

    private void CurrentMatchViewModel_StateUpdated(object? sender, EventArgs e)
    {
        RenderHomeFromCurrentMatch();
    }

    private void RenderHomeFromCurrentMatch()
    {
        switch (_currentMatchViewModel.Status)
        {
            case MatchSnapshotReadStatus.Success:
                RenderHomeConnected();
                break;
            case MatchSnapshotReadStatus.GameLoading:
            case MatchSnapshotReadStatus.NoActiveMatch:
                RenderHomeWaiting();
                break;
            case MatchSnapshotReadStatus.Timeout:
            case MatchSnapshotReadStatus.InvalidData:
            case MatchSnapshotReadStatus.Failed:
                RenderHomeError();
                break;
            default:
                RenderHomeWaiting();
                break;
        }
    }

    private void RenderHomeWaiting()
    {
        HomeConnectionTitleTextBlock.Text = _currentMatchViewModel.ConnectionTitle;
        HomeConnectionDetailsTextBlock.Text = _currentMatchViewModel.ConnectionDetails;
        HomeHeroTitleTextBlock.Text = _localization.Get("Home.Waiting.Title");
        HomeHeroDescriptionTextBlock.Text = _localization.Get("Home.Waiting.Description");
        HomeLiveDataValueTextBlock.Text = _localization.Get("Home.Status.Waiting");
        HomeMatchStatusValueTextBlock.Text = _localization.Get("Home.Status.NotFound");
        HomeEngineStatusTextBlock.Text = _currentMatchViewModel.Status
            == MatchSnapshotReadStatus.GameLoading
                ? _localization.Get("Home.Engine.Loading")
                : _localization.Get("Home.Engine.Waiting");
        HomeMatchNavBadgeTextBlock.Text = "—";
        HomeGameTimePanel.Visibility = Visibility.Collapsed;
        ApplyHomeConnectionColors("#1E2E45", "#8393A8", "#B5C5D8", "#D6B766");
        UpdateTitleBarContext();
    }

    private void RenderHomeConnected()
    {
        var playersText = _localization.Plural(
            "Count.Player",
            _currentMatchViewModel.ParticipantCount);
        HomeConnectionTitleTextBlock.Text = _localization.Get("Connection.MatchConnected");
        HomeConnectionDetailsTextBlock.Text = _currentMatchViewModel.ConnectionDetails;
        HomeHeroTitleTextBlock.Text = _localization.Get("Home.Connected.Title");
        HomeHeroDescriptionTextBlock.Text = _localization.Format(
            "Home.Connected.Description",
            _currentMatchViewModel.ActiveChampion,
            playersText);
        HomeLiveDataValueTextBlock.Text = _localization.Get("Home.Status.Connected");
        HomeMatchStatusValueTextBlock.Text = _localization.Get("Home.Status.Active");
        HomeEngineStatusTextBlock.Text = _localization.Get("Home.Engine.Connected");
        HomeMatchNavBadgeTextBlock.Text = "LIVE";
        HomeGameTimeTextBlock.Text = _currentMatchViewModel.GameTime;
        HomeGameTimePanel.Visibility = Visibility.Visible;
        ApplyHomeConnectionColors("#17342D", "#64D5A4", "#87E1BB", "#67CDA4");
        UpdateTitleBarContext(_currentMatchViewModel.GameTime);
    }

    private void RenderHomeError()
    {
        HomeConnectionTitleTextBlock.Text = _currentMatchViewModel.ConnectionTitle;
        HomeConnectionDetailsTextBlock.Text = _currentMatchViewModel.ConnectionDetails;
        HomeHeroTitleTextBlock.Text = _localization.Get("Home.Error.Title");
        HomeHeroDescriptionTextBlock.Text = _localization.Format(
            "Home.Error.Description",
            _currentMatchViewModel.ConnectionTitle);
        HomeLiveDataValueTextBlock.Text = _localization.Get("Home.Status.Error");
        HomeMatchStatusValueTextBlock.Text = _localization.Get("Home.Status.NotFound");
        HomeEngineStatusTextBlock.Text = _localization.Get("Home.Engine.Error");
        HomeMatchNavBadgeTextBlock.Text = "—";
        HomeGameTimePanel.Visibility = Visibility.Collapsed;
        ApplyHomeConnectionColors("#3A3020", "#E8BB58", "#F0CC7A", "#E8BB58");
        UpdateTitleBarContext();
    }

    private void ApplyHomeConnectionColors(
        string badgeBackground,
        string statusDot,
        string statusText,
        string valueText)
    {
        HomeConnectionBadgeBorder.Background = ToBrush(badgeBackground);
        HomeConnectionDot.Fill = ToBrush(statusDot);
        HomeEngineStatusDot.Fill = ToBrush(statusDot);
        HomeConnectionTitleTextBlock.Foreground = ToBrush(statusText);
        HomeLiveDataValueTextBlock.Foreground = ToBrush(valueText);
    }
}
