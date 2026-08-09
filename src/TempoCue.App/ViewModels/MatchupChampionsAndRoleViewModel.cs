using TempoCue.Application;
using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Выбор пары чемпионов и роли для второго шага редактора матчапа.</summary>
public sealed class MatchupChampionsAndRoleViewModel : ObservableObject
{
    private readonly IChampionCatalog _championCatalog;
    private readonly LocalizationService _localization;
    private IReadOnlyList<ChampionOptionViewModel> _champions = [];
    private ChampionOptionViewModel? _selectedPlayerChampion;
    private ChampionOptionViewModel? _selectedOpponentChampion;
    private MatchupLaneRole _selectedRole = MatchupLaneRole.Top;
    private string _languageCode;
    private string? _loadedLanguageCode;
    private CatalogViewState _catalogState = CatalogViewState.NotLoaded;
    private ChampionPickerTarget _pickerTarget;
    private string _pickerSearch = string.Empty;

    public MatchupChampionsAndRoleViewModel(
        IChampionCatalog championCatalog,
        LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(championCatalog);
        ArgumentNullException.ThrowIfNull(localization);
        _championCatalog = championCatalog;
        _localization = localization;
        _languageCode = localization.CurrentLanguageCode;
    }

    public IReadOnlyList<ChampionOptionViewModel> Champions => _champions;

    public IReadOnlyList<ChampionOptionViewModel> FilteredPickerChampions =>
        FilterAllChampions(PickerSearch);

    public ChampionPickerTarget PickerTarget
    {
        get => _pickerTarget;
        private set
        {
            if (SetProperty(ref _pickerTarget, value))
            {
                OnPropertyChanged(nameof(IsPickerOpen));
            }
        }
    }

    public bool IsPickerOpen => PickerTarget != ChampionPickerTarget.None;

    public string PickerSearch
    {
        get => _pickerSearch;
        set
        {
            if (SetProperty(ref _pickerSearch, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(FilteredPickerChampions));
                OnPropertyChanged(nameof(HasPickerResults));
                OnPropertyChanged(nameof(PickerResultCountText));
            }
        }
    }

    public bool HasPickerResults => FilteredPickerChampions.Count > 0;

    public string CatalogStatusText => _localization.Format(
        "Coaching.MatchupEditor.Champions.CatalogLoaded",
        Champions.Count);

    public string PickerResultCountText => _localization.Plural(
        "Count.Champion",
        FilteredPickerChampions.Count);

    public string ReadinessSummary => _localization.Format(
        "Coaching.MatchupEditor.Champions.ReadinessSummary",
        SelectedPlayerChampion?.DisplayName ?? string.Empty,
        SelectedOpponentChampion?.DisplayName ?? string.Empty,
        GetRoleLabel());

    public CatalogViewState CatalogState
    {
        get => _catalogState;
        private set
        {
            if (SetProperty(ref _catalogState, value))
            {
                OnPropertyChanged(nameof(IsCatalogReady));
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(HasLoadError));
                OnPropertyChanged(nameof(IsUsingCache));
                OnPropertyChanged(nameof(IsComplete));
            }
        }
    }

    public bool IsCatalogReady =>
        CatalogState is CatalogViewState.Ready or CatalogViewState.Cached;

    public bool IsLoading => CatalogState == CatalogViewState.Loading;

    public bool HasLoadError => CatalogState == CatalogViewState.Failed;

    public bool IsUsingCache => CatalogState == CatalogViewState.Cached;

    public ChampionOptionViewModel? SelectedPlayerChampion
    {
        get => _selectedPlayerChampion;
        set
        {
            if (SetProperty(ref _selectedPlayerChampion, value))
            {
                NotifySelectionStateChanged();
            }
        }
    }

    public ChampionOptionViewModel? SelectedOpponentChampion
    {
        get => _selectedOpponentChampion;
        set
        {
            if (SetProperty(ref _selectedOpponentChampion, value))
            {
                NotifySelectionStateChanged();
            }
        }
    }

    public MatchupLaneRole SelectedRole
    {
        get => _selectedRole;
        set
        {
            if (SetProperty(ref _selectedRole, value))
            {
                OnPropertyChanged(nameof(IsComplete));
                OnPropertyChanged(nameof(ReadinessSummary));
            }
        }
    }

    public bool HasSameChampion =>
        SelectedPlayerChampion is not null
        && SelectedOpponentChampion is not null
        && SelectedPlayerChampion.Id == SelectedOpponentChampion.Id;

    public bool IsComplete =>
        IsCatalogReady
        &&
        SelectedPlayerChampion is not null
        && SelectedOpponentChampion is not null
        && !HasSameChampion;

    public void UpdateLanguage(string languageCode)
    {
        if (string.Equals(_languageCode, languageCode, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _languageCode = languageCode;
        ClosePicker();
        CatalogState = CatalogViewState.NotLoaded;
        OnPropertyChanged(nameof(CatalogStatusText));
        OnPropertyChanged(nameof(PickerResultCountText));
        OnPropertyChanged(nameof(ReadinessSummary));
    }

    public void OpenPlayerPicker() => OpenPicker(ChampionPickerTarget.Player);

    public void OpenOpponentPicker() => OpenPicker(ChampionPickerTarget.Opponent);

    public void ClosePicker()
    {
        PickerSearch = string.Empty;
        PickerTarget = ChampionPickerTarget.None;
    }

    public void SelectPickerChampion(ChampionOptionViewModel champion)
    {
        ArgumentNullException.ThrowIfNull(champion);

        if (PickerTarget == ChampionPickerTarget.Player)
        {
            SelectedPlayerChampion = champion;
        }
        else if (PickerTarget == ChampionPickerTarget.Opponent)
        {
            SelectedOpponentChampion = champion;
        }

        ClosePicker();
    }

    public async Task EnsureCatalogLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (IsLoading
            || (IsCatalogReady
                && string.Equals(
                    _loadedLanguageCode,
                    _languageCode,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        CatalogState = CatalogViewState.Loading;
        var requestedLanguageCode = _languageCode;
        var playerChampionId = SelectedPlayerChampion?.Id;
        var opponentChampionId = SelectedOpponentChampion?.Id;
        var result = await _championCatalog.LoadAsync(requestedLanguageCode, cancellationToken);

        if (!string.Equals(
                requestedLanguageCode,
                _languageCode,
                StringComparison.OrdinalIgnoreCase))
        {
            CatalogState = CatalogViewState.NotLoaded;
            return;
        }

        if (!result.IsAvailable)
        {
            _champions = [];
            _loadedLanguageCode = null;
            NotifyCatalogChanged();
            CatalogState = CatalogViewState.Failed;
            return;
        }

        _champions = result.Champions
            .Select(champion => new ChampionOptionViewModel(
                champion.Id,
                champion.DisplayName,
                champion.ImageUri))
            .ToArray();
        _loadedLanguageCode = requestedLanguageCode;
        SelectedPlayerChampion = FindChampion(playerChampionId);
        SelectedOpponentChampion = FindChampion(opponentChampionId);
        NotifyCatalogChanged();
        CatalogState = result.Source == ChampionCatalogSource.Cache
            ? CatalogViewState.Cached
            : CatalogViewState.Ready;
    }

    public Task RetryCatalogLoadAsync(CancellationToken cancellationToken = default)
    {
        CatalogState = CatalogViewState.NotLoaded;
        return EnsureCatalogLoadedAsync(cancellationToken);
    }

    private IReadOnlyList<ChampionOptionViewModel> FilterAllChampions(string search)
    {
        var normalizedSearch = search.Trim();
        if (normalizedSearch.Length == 0)
        {
            return Champions;
        }

        return Champions
            .Where(champion => champion.DisplayName.Contains(
                normalizedSearch,
                StringComparison.CurrentCultureIgnoreCase))
            .ToArray();
    }

    private void OpenPicker(ChampionPickerTarget target)
    {
        if (!IsCatalogReady)
        {
            return;
        }

        PickerSearch = string.Empty;
        PickerTarget = target;
    }

    private ChampionOptionViewModel? FindChampion(string? championId) =>
        championId is null
            ? null
            : Champions.FirstOrDefault(champion =>
                string.Equals(champion.Id, championId, StringComparison.OrdinalIgnoreCase));

    private void NotifyCatalogChanged()
    {
        OnPropertyChanged(nameof(Champions));
        OnPropertyChanged(nameof(FilteredPickerChampions));
        OnPropertyChanged(nameof(HasPickerResults));
        OnPropertyChanged(nameof(CatalogStatusText));
        OnPropertyChanged(nameof(PickerResultCountText));
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(HasSameChampion));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(ReadinessSummary));
    }

    private string GetRoleLabel() => SelectedRole switch
    {
        MatchupLaneRole.Top => "TOP",
        MatchupLaneRole.Jungle => "JUNGLE",
        MatchupLaneRole.Mid => "MID",
        MatchupLaneRole.Adc => "ADC",
        MatchupLaneRole.Support => "SUPPORT",
        _ => _localization.Get("Coaching.MatchupEditor.Champions.Role.Any")
    };
}
