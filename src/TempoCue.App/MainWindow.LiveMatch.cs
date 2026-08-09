using System.Windows;
using TempoCue.Application;

namespace TempoCue.App;

/// <summary>
/// Обновляет общие индикаторы оболочки из состояния активного матча.
/// Самостоятельные страницы используют собственные ViewModel и binding.
/// </summary>
public partial class MainWindow
{
    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _currentMatchViewModel.StateUpdated -= CurrentMatchViewModel_StateUpdated;
        HomeContentPage.OpenCurrentMatchRequested -= HomeContentPage_OpenCurrentMatchRequested;
    }

    private void CurrentMatchViewModel_StateUpdated(object? sender, EventArgs e)
    {
        RenderShellFromCurrentMatch();
    }

    private void RenderShellFromCurrentMatch()
    {
        switch (_currentMatchViewModel.Status)
        {
            case MatchSnapshotReadStatus.Success:
                RenderShellConnected();
                break;
            case MatchSnapshotReadStatus.GameLoading:
            case MatchSnapshotReadStatus.NoActiveMatch:
                RenderShellWaiting();
                break;
            case MatchSnapshotReadStatus.Timeout:
            case MatchSnapshotReadStatus.InvalidData:
            case MatchSnapshotReadStatus.Failed:
                RenderShellError();
                break;
            default:
                RenderShellWaiting();
                break;
        }
    }

    private void RenderShellWaiting()
    {
        HomeConnectionTitleTextBlock.Text = _currentMatchViewModel.ConnectionTitle;
        HomeConnectionDetailsTextBlock.Text = _currentMatchViewModel.ConnectionDetails;
        HomeEngineStatusTextBlock.Text = _currentMatchViewModel.Status
            == MatchSnapshotReadStatus.GameLoading
                ? _localization.Get("Home.Engine.Loading")
                : _localization.Get("Home.Engine.Waiting");
        HomeMatchNavBadgeTextBlock.Text = "—";
        HomeGameTimePanel.Visibility = Visibility.Collapsed;
        ApplyShellConnectionColors("#1E2E45", "#8393A8", "#B5C5D8");
        UpdateTitleBarContext();
    }

    private void RenderShellConnected()
    {
        HomeConnectionTitleTextBlock.Text = _localization.Get("Connection.MatchConnected");
        HomeConnectionDetailsTextBlock.Text = _currentMatchViewModel.ConnectionDetails;
        HomeEngineStatusTextBlock.Text = _localization.Get("Home.Engine.Connected");
        HomeMatchNavBadgeTextBlock.Text = "LIVE";
        HomeGameTimeTextBlock.Text = _currentMatchViewModel.GameTime;
        HomeGameTimePanel.Visibility = Visibility.Visible;
        ApplyShellConnectionColors("#17342D", "#64D5A4", "#87E1BB");
        UpdateTitleBarContext(_currentMatchViewModel.GameTime);
    }

    private void RenderShellError()
    {
        HomeConnectionTitleTextBlock.Text = _currentMatchViewModel.ConnectionTitle;
        HomeConnectionDetailsTextBlock.Text = _currentMatchViewModel.ConnectionDetails;
        HomeEngineStatusTextBlock.Text = _localization.Get("Home.Engine.Error");
        HomeMatchNavBadgeTextBlock.Text = "—";
        HomeGameTimePanel.Visibility = Visibility.Collapsed;
        ApplyShellConnectionColors("#3A3020", "#E8BB58", "#F0CC7A");
        UpdateTitleBarContext();
    }

    private void ApplyShellConnectionColors(
        string badgeBackground,
        string statusDot,
        string statusText)
    {
        HomeConnectionBadgeBorder.Background = ToBrush(badgeBackground);
        HomeConnectionDot.Fill = ToBrush(statusDot);
        HomeEngineStatusDot.Fill = ToBrush(statusDot);
        HomeConnectionTitleTextBlock.Foreground = ToBrush(statusText);
    }
}
