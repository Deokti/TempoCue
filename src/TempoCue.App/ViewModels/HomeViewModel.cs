using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Готовит состояние главной страницы на основе общего снимка активного матча.
/// Не управляет навигацией и не обращается к элементам WPF.
/// </summary>
public sealed class HomeViewModel : ObservableObject, IDisposable
{
    private readonly CurrentMatchViewModel _currentMatch;
    private readonly LocalizationService _localization;

    private MatchSnapshotReadStatus _status;
    private string _heroTitle = string.Empty;
    private string _heroDescription = string.Empty;
    private string _liveDataValue = string.Empty;
    private string _matchStatusValue = string.Empty;
    private bool _isDisposed;

    public HomeViewModel(
        CurrentMatchViewModel currentMatch,
        LocalizationService localization)
    {
        _currentMatch = currentMatch;
        _localization = localization;
        _currentMatch.StateUpdated += CurrentMatch_StateUpdated;
        _localization.PropertyChanged += Localization_PropertyChanged;
        Render();
    }

    public MatchSnapshotReadStatus Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string HeroTitle
    {
        get => _heroTitle;
        private set => SetProperty(ref _heroTitle, value);
    }

    public string HeroDescription
    {
        get => _heroDescription;
        private set => SetProperty(ref _heroDescription, value);
    }

    public string LiveDataValue
    {
        get => _liveDataValue;
        private set => SetProperty(ref _liveDataValue, value);
    }

    public string MatchStatusValue
    {
        get => _matchStatusValue;
        private set => SetProperty(ref _matchStatusValue, value);
    }

    public Task RefreshAsync() => _currentMatch.RefreshAsync();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _currentMatch.StateUpdated -= CurrentMatch_StateUpdated;
        _localization.PropertyChanged -= Localization_PropertyChanged;
    }

    private void CurrentMatch_StateUpdated(object? sender, EventArgs e) => Render();

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e) => Render();

    private void Render()
    {
        Status = _currentMatch.Status;

        switch (_currentMatch.Status)
        {
            case MatchSnapshotReadStatus.Success:
                HeroTitle = _localization.Get("Home.Connected.Title");
                HeroDescription = _localization.Format(
                    "Home.Connected.Description",
                    _currentMatch.ActiveChampion,
                    _localization.Plural("Count.Player", _currentMatch.ParticipantCount));
                LiveDataValue = _localization.Get("Home.Status.Connected");
                MatchStatusValue = _localization.Get("Home.Status.Active");
                break;
            case MatchSnapshotReadStatus.Timeout:
            case MatchSnapshotReadStatus.InvalidData:
            case MatchSnapshotReadStatus.Failed:
                HeroTitle = _localization.Get("Home.Error.Title");
                HeroDescription = _localization.Format(
                    "Home.Error.Description",
                    _currentMatch.ConnectionTitle);
                LiveDataValue = _localization.Get("Home.Status.Error");
                MatchStatusValue = _localization.Get("Home.Status.NotFound");
                break;
            default:
                HeroTitle = _localization.Get("Home.Waiting.Title");
                HeroDescription = _localization.Get("Home.Waiting.Description");
                LiveDataValue = _localization.Get("Home.Status.Waiting");
                MatchStatusValue = _localization.Get("Home.Status.NotFound");
                break;
        }
    }
}
