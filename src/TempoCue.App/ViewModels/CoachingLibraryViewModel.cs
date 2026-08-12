using System.ComponentModel;
using System.IO;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Загружает, локализует и фильтрует карточки библиотеки коучинга.</summary>
public sealed class CoachingLibraryViewModel : ObservableObject, IDisposable
{
    private readonly IReadOnlyList<CoachPack> _builtInPacks;
    private readonly LocalizationService _localization;
    private readonly IChampionCatalog _championCatalog;
    private readonly LoadCoachPackLibraryUseCase _loadCoachPackLibrary;
    private readonly SetCoachPackStateUseCase _setCoachPackState;
    private readonly DeleteCoachPackUseCase _deleteCoachPack;
    private readonly LoadMapControlPackLibraryUseCase _loadMapControlPackLibrary;
    private readonly SetMapControlPackStateUseCase _setMapControlPackState;
    private readonly DeleteMapControlPackUseCase _deleteMapControlPack;
    private readonly CoachingPackCardFactory _cardFactory;
    private readonly MapControlPackCardFactory _mapControlCardFactory;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private IReadOnlyList<LocalCoachPackEntry> _localEntries = [];
    private IReadOnlyDictionary<string, ChampionCatalogEntry> _champions =
        new Dictionary<string, ChampionCatalogEntry>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<CoachingPackCardViewModel> _allCards = [];
    private IReadOnlyList<CoachingPackCardViewModel> _matchupCards = [];
    private IReadOnlyList<CoachingPackCardViewModel> _myPackCards = [];
    private IReadOnlyList<LocalMapControlPackEntry> _mapControlEntries = [];
    private IReadOnlyDictionary<string, LocalCoachPackState> _mapControlPackStates =
        new Dictionary<string, LocalCoachPackState>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<MapControlPackCardViewModel> _allMapControlCards = [];
    private IReadOnlyList<MapControlPackCardViewModel> _mapControlCards = [];
    private string _librarySummary = string.Empty;
    private string _searchText = string.Empty;
    private string _libraryMessage = string.Empty;
    private bool _isLibraryMessageSuccess;
    private bool _isLibraryLoading;
    private bool _isLibraryLoaded;
    private bool _isChampionCatalogLoaded;
    private IReadOnlyDictionary<string, LocalCoachPackState> _packStates =
        new Dictionary<string, LocalCoachPackState>(StringComparer.OrdinalIgnoreCase);
    private bool _isDisposed;

    public CoachingLibraryViewModel(
        IReadOnlyList<CoachPack> builtInPacks,
        LocalizationService localization,
        IChampionCatalog championCatalog,
        LoadCoachPackLibraryUseCase loadCoachPackLibrary,
        SetCoachPackStateUseCase setCoachPackState,
        DeleteCoachPackUseCase deleteCoachPack,
        LoadMapControlPackLibraryUseCase loadMapControlPackLibrary,
        SetMapControlPackStateUseCase setMapControlPackState,
        DeleteMapControlPackUseCase deleteMapControlPack)
    {
        ArgumentNullException.ThrowIfNull(builtInPacks);
        _builtInPacks = builtInPacks.ToArray();
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _championCatalog = championCatalog ?? throw new ArgumentNullException(nameof(championCatalog));
        _loadCoachPackLibrary = loadCoachPackLibrary
            ?? throw new ArgumentNullException(nameof(loadCoachPackLibrary));
        _setCoachPackState = setCoachPackState
            ?? throw new ArgumentNullException(nameof(setCoachPackState));
        _deleteCoachPack = deleteCoachPack
            ?? throw new ArgumentNullException(nameof(deleteCoachPack));
        _loadMapControlPackLibrary = loadMapControlPackLibrary
            ?? throw new ArgumentNullException(nameof(loadMapControlPackLibrary));
        _setMapControlPackState = setMapControlPackState
            ?? throw new ArgumentNullException(nameof(setMapControlPackState));
        _deleteMapControlPack = deleteMapControlPack
            ?? throw new ArgumentNullException(nameof(deleteMapControlPack));
        _cardFactory = new CoachingPackCardFactory(localization);
        _mapControlCardFactory = new MapControlPackCardFactory(localization);
        _localization.PropertyChanged += Localization_PropertyChanged;
        RebuildCards();
    }

    public IReadOnlyList<CoachingPackCardViewModel> MatchupCards
    {
        get => _matchupCards;
        private set
        {
            if (SetProperty(ref _matchupCards, value))
            {
                OnPropertyChanged(nameof(HasMatchupCards));
            }
        }
    }

    public IReadOnlyList<CoachingPackCardViewModel> MyPackCards
    {
        get => _myPackCards;
        private set
        {
            if (SetProperty(ref _myPackCards, value))
            {
                OnPropertyChanged(nameof(HasMyPackCards));
            }
        }
    }

    public bool HasMatchupCards => MatchupCards.Count > 0;

    public bool HasMyPackCards => MyPackCards.Count > 0;

    public IReadOnlyList<MapControlPackCardViewModel> MapControlCards
    {
        get => _mapControlCards;
        private set
        {
            if (SetProperty(ref _mapControlCards, value))
            {
                OnPropertyChanged(nameof(HasMapControlCards));
            }
        }
    }

    public bool HasMapControlCards => MapControlCards.Count > 0;

    public int TotalPackCount => _allCards.Count + _allMapControlCards.Count;

    public int EnabledPackCount => _allCards.Count(static card => card.IsEnabled)
                                   + _allMapControlCards.Count(static card => card.IsEnabled);

    public string LibrarySummary
    {
        get => _librarySummary;
        private set => SetProperty(ref _librarySummary, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplySearch();
            }
        }
    }

    public string LibraryMessage
    {
        get => _libraryMessage;
        private set
        {
            if (SetProperty(ref _libraryMessage, value))
            {
                OnPropertyChanged(nameof(HasLibraryMessage));
            }
        }
    }

    public bool HasLibraryMessage => !string.IsNullOrWhiteSpace(LibraryMessage);

    public bool IsLibraryMessageSuccess
    {
        get => _isLibraryMessageSuccess;
        private set => SetProperty(ref _isLibraryMessageSuccess, value);
    }

    /// <summary>
    /// Сообщает composition root, что наборы или их локальные состояния перечитаны.
    /// Активный движок использует событие только для инвалидирования своего кеша.
    /// </summary>
    public event EventHandler? LibraryChanged;

    public bool IsLibraryLoading
    {
        get => _isLibraryLoading;
        private set => SetProperty(ref _isLibraryLoading, value);
    }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        _isLibraryLoaded
            ? Task.CompletedTask
            : RefreshAsync(cancellationToken);

    public CoachingPackCardViewModel? FindCard(string packId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        return _allCards.FirstOrDefault(card => card.Pack.Id == packId);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var lockTaken = false;
        try
        {
            await _refreshLock.WaitAsync(cancellationToken);
            lockTaken = true;
            IsLibraryLoading = true;
            IsLibraryMessageSuccess = false;
            LibraryMessage = string.Empty;

            var matchupLoad = _loadCoachPackLibrary.ExecuteAsync(cancellationToken);
            var mapControlLoad = _loadMapControlPackLibrary.ExecuteAsync(cancellationToken);
            await Task.WhenAll(matchupLoad, mapControlLoad);
            var snapshot = await matchupLoad;
            var mapControlSnapshot = await mapControlLoad;
            _localEntries = snapshot.Entries;
            _packStates = snapshot.PackStates;
            _mapControlEntries = mapControlSnapshot.Entries;
            _mapControlPackStates = mapControlSnapshot.PackStates;
            _isLibraryLoaded = true;
            var invalidFileCount = snapshot.InvalidFileCount + mapControlSnapshot.InvalidFileCount;
            LibraryMessage = invalidFileCount > 0
                ? _localization.Plural("Count.CoachPackInvalid", invalidFileCount)
                : string.Empty;
            RebuildCards();
            LibraryChanged?.Invoke(this, EventArgs.Empty);

            if (!_isChampionCatalogLoaded)
            {
                var catalog = await _championCatalog.LoadAsync(
                    _localization.CurrentLanguageCode,
                    cancellationToken);
                _champions = catalog.Champions.ToDictionary(
                    static champion => champion.Id,
                    StringComparer.OrdinalIgnoreCase);
                _isChampionCatalogLoaded = true;
                RebuildCards();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            LibraryMessage = _localization.Get("Coaching.Library.LoadFailed");
        }
        finally
        {
            if (lockTaken)
            {
                IsLibraryLoading = false;
                _refreshLock.Release();
            }
        }
    }

    public async Task<bool> SetPackEnabledAsync(
        CoachingPackCardViewModel card,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        IsLibraryMessageSuccess = false;
        try
        {
            await _setCoachPackState.ExecuteAsync(
                card.Pack.Id,
                isEnabled ? LocalCoachPackState.Enabled : LocalCoachPackState.Disabled,
                cancellationToken);
            await RefreshAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            LibraryMessage = _localization.Get("Coaching.Library.StateChangeFailed");
            return false;
        }
    }

    public MapControlPackCardViewModel? FindMapControlCard(string packId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packId);
        return _allMapControlCards.FirstOrDefault(card => card.Pack.Id == packId);
    }

    public async Task<bool> SetMapControlPackEnabledAsync(
        MapControlPackCardViewModel card,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        IsLibraryMessageSuccess = false;
        try
        {
            await _setMapControlPackState.ExecuteAsync(
                card.Pack.Id,
                isEnabled ? LocalCoachPackState.Enabled : LocalCoachPackState.Disabled,
                cancellationToken);
            await RefreshAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            LibraryMessage = _localization.Get("Coaching.Library.StateChangeFailed");
            return false;
        }
    }

    public async Task<bool> DeletePackAsync(
        CoachingPackCardViewModel card,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        IsLibraryMessageSuccess = false;
        if (!card.CanDelete)
        {
            return false;
        }

        try
        {
            if (!await _deleteCoachPack.ExecuteAsync(card.Pack.Id, cancellationToken))
            {
                LibraryMessage = _localization.Get("Coaching.Library.DeleteFailed");
                return false;
            }

            await RefreshAsync(cancellationToken);
            IsLibraryMessageSuccess = true;
            LibraryMessage = _localization.Format("Coaching.Library.Deleted", card.Title);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            LibraryMessage = _localization.Get("Coaching.Library.DeleteFailed");
            return false;
        }
    }

    public void ReportImportSuccess(string packName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packName);
        IsLibraryMessageSuccess = true;
        LibraryMessage = _localization.Format("Coaching.Library.Imported", packName);
    }

    public void ReportExportResult(string packName, bool succeeded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packName);
        IsLibraryMessageSuccess = succeeded;
        LibraryMessage = succeeded
            ? _localization.Format("Coaching.Library.Exported", packName)
            : _localization.Get("Coaching.Library.ExportFailed");
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= Localization_PropertyChanged;
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        IsLibraryMessageSuccess = false;
        LibraryMessage = string.Empty;
        RebuildCards();
    }

    private void RebuildCards()
    {
        var cards = _builtInPacks.Select(pack => _cardFactory.Create(
                pack,
                CoachingPackOrigin.BuiltIn,
                _packStates.GetValueOrDefault(pack.Id, LocalCoachPackState.Enabled),
                _champions))
            .Concat(_localEntries.Select(entry => _cardFactory.Create(
                entry.Pack,
                entry.Origin == LocalCoachPackOrigin.CreatedByUser
                    ? CoachingPackOrigin.CreatedByUser
                    : CoachingPackOrigin.Imported,
                entry.State,
                _champions)))
            .ToArray();
        _allCards = cards;
        _allMapControlCards = _mapControlEntries
            .Select(entry => _mapControlCardFactory.Create(
                entry.Pack,
                entry.Origin == LocalCoachPackOrigin.CreatedByUser
                    ? CoachingPackOrigin.CreatedByUser
                    : CoachingPackOrigin.Imported,
                _mapControlPackStates.GetValueOrDefault(entry.Pack.Id, entry.State)))
            .ToArray();
        OnPropertyChanged(nameof(TotalPackCount));
        OnPropertyChanged(nameof(EnabledPackCount));
        LibrarySummary = string.Join(
            " · ",
            _localization.Plural("Count.CoachPack", TotalPackCount),
            _localization.Plural("Count.CoachPackEnabled", EnabledPackCount),
            _localization.Plural(
                "Count.CoachPackCreated",
                cards.Count(static card => card.IsCreatedByUser)
                + _allMapControlCards.Count(static card => card.IsCreatedByUser)));
        ApplySearch();
    }

    private void ApplySearch()
    {
        var filteredCards = _allCards
            .Where(MatchesSearch)
            .ToArray();
        MatchupCards = filteredCards;
        MapControlCards = _allMapControlCards
            .Where(MatchesMapControlSearch)
            .ToArray();
        MyPackCards = filteredCards
            .Where(static card => card.IsCreatedByUser)
            .ToArray();
    }

    public async Task<bool> DeleteMapControlPackAsync(
        MapControlPackCardViewModel card,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
        IsLibraryMessageSuccess = false;
        if (!card.CanDelete)
        {
            return false;
        }

        try
        {
            if (!await _deleteMapControlPack.ExecuteAsync(card.Pack.Id, cancellationToken))
            {
                LibraryMessage = _localization.Get("Coaching.Library.DeleteFailed");
                return false;
            }

            await RefreshAsync(cancellationToken);
            IsLibraryMessageSuccess = true;
            LibraryMessage = _localization.Format("Coaching.Library.Deleted", card.Title);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (IsExpectedLibraryException(exception))
        {
            LibraryMessage = _localization.Get("Coaching.Library.DeleteFailed");
            return false;
        }
    }

    private bool MatchesMapControlSearch(MapControlPackCardViewModel card)
    {
        var search = SearchText.Trim();
        return search.Length == 0
               || card.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
               || card.Details.Contains(search, StringComparison.CurrentCultureIgnoreCase)
               || card.Pack.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private bool MatchesSearch(CoachingPackCardViewModel card)
    {
        var search = SearchText.Trim();
        return search.Length == 0
               || card.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
               || card.Details.Contains(search, StringComparison.CurrentCultureIgnoreCase)
               || card.Pack.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private static bool IsExpectedLibraryException(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException;
}
