using System.ComponentModel;
using System.IO;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Загружает, локализует и фильтрует карточки библиотеки коучинга.</summary>
public sealed class CoachingLibraryViewModel : ObservableObject, IDisposable
{
    private readonly CoachPack _builtInPack;
    private readonly LocalizationService _localization;
    private readonly IChampionCatalog _championCatalog;
    private readonly LoadCoachPackLibraryUseCase _loadCoachPackLibrary;
    private readonly SetCoachPackStateUseCase _setCoachPackState;
    private readonly CoachingPackCardFactory _cardFactory;

    private IReadOnlyList<LocalCoachPackEntry> _localEntries = [];
    private IReadOnlyDictionary<string, ChampionCatalogEntry> _champions =
        new Dictionary<string, ChampionCatalogEntry>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<CoachingPackCardViewModel> _allCards = [];
    private IReadOnlyList<CoachingPackCardViewModel> _matchupCards = [];
    private IReadOnlyList<CoachingPackCardViewModel> _myPackCards = [];
    private string _librarySummary = string.Empty;
    private string _searchText = string.Empty;
    private string _libraryMessage = string.Empty;
    private bool _isLibraryLoading;
    private bool _isLibraryLoaded;
    private bool _isChampionCatalogLoaded;
    private LocalCoachPackState _builtInPackState = LocalCoachPackState.Enabled;
    private bool _isDisposed;

    public CoachingLibraryViewModel(
        CoachPack builtInPack,
        LocalizationService localization,
        IChampionCatalog championCatalog,
        LoadCoachPackLibraryUseCase loadCoachPackLibrary,
        SetCoachPackStateUseCase setCoachPackState)
    {
        _builtInPack = builtInPack ?? throw new ArgumentNullException(nameof(builtInPack));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _championCatalog = championCatalog ?? throw new ArgumentNullException(nameof(championCatalog));
        _loadCoachPackLibrary = loadCoachPackLibrary
            ?? throw new ArgumentNullException(nameof(loadCoachPackLibrary));
        _setCoachPackState = setCoachPackState
            ?? throw new ArgumentNullException(nameof(setCoachPackState));
        _cardFactory = new CoachingPackCardFactory(localization);
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

    public bool IsLibraryLoading
    {
        get => _isLibraryLoading;
        private set => SetProperty(ref _isLibraryLoading, value);
    }

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        _isLibraryLoaded
            ? Task.CompletedTask
            : RefreshAsync(cancellationToken);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        IsLibraryLoading = true;
        LibraryMessage = string.Empty;
        try
        {
            var snapshot = await _loadCoachPackLibrary.ExecuteAsync(cancellationToken);
            _localEntries = snapshot.Entries;
            _builtInPackState = snapshot.PackStates.GetValueOrDefault(
                _builtInPack.Id,
                LocalCoachPackState.Enabled);
            _isLibraryLoaded = true;
            LibraryMessage = snapshot.InvalidFileCount > 0
                ? _localization.Plural("Count.CoachPackInvalid", snapshot.InvalidFileCount)
                : string.Empty;
            RebuildCards();

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
            IsLibraryLoading = false;
        }
    }

    public async Task<bool> SetPackEnabledAsync(
        CoachingPackCardViewModel card,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(card);
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

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        _localization.PropertyChanged -= Localization_PropertyChanged;
    }

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RebuildCards();

    private void RebuildCards()
    {
        var cards = new List<CoachingPackCardViewModel>
        {
            _cardFactory.Create(
                _builtInPack,
                CoachingPackOrigin.BuiltIn,
                _builtInPackState,
                _champions)
        };
        cards.AddRange(_localEntries.Select(entry => _cardFactory.Create(
            entry.Pack,
            CoachingPackOrigin.CreatedByUser,
            entry.State,
            _champions)));
        _allCards = cards;
        LibrarySummary = string.Join(
            " · ",
            _localization.Plural("Count.CoachPack", cards.Count),
            _localization.Plural("Count.CoachPackEnabled", cards.Count(static card => card.IsEnabled)),
            _localization.Plural("Count.CoachPackCreated", cards.Count(static card => card.IsCreatedByUser)));
        ApplySearch();
    }

    private void ApplySearch()
    {
        var filteredCards = _allCards
            .Where(MatchesSearch)
            .ToArray();
        MatchupCards = filteredCards;
        MyPackCards = filteredCards
            .Where(static card => card.IsCreatedByUser)
            .ToArray();
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
