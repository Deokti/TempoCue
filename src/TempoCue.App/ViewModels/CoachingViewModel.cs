using System.ComponentModel;
using System.IO;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Состояние библиотеки коучинга. На первой итерации показывает встроенный
/// матчап; операции импорта, хранения и редактирования будут отдельными сценариями.
/// </summary>
public sealed class CoachingViewModel : ObservableObject, IDisposable
{
    private readonly CoachPack _pack;
    private readonly LocalizationService _localization;
    private readonly IChampionCatalog _championCatalog;
    private readonly LoadCoachPackLibraryUseCase _loadCoachPackLibrary;
    private readonly SetCoachPackStateUseCase _setCoachPackState;

    private IReadOnlyList<CoachingRuleViewModel> _rules = [];
    private IReadOnlyList<LocalCoachPackEntry> _localEntries = [];
    private IReadOnlyDictionary<string, ChampionCatalogEntry> _champions =
        new Dictionary<string, ChampionCatalogEntry>(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<CoachingPackCardViewModel> _matchupCards = [];
    private IReadOnlyList<CoachingPackCardViewModel> _myPackCards = [];
    private string _rulesSummary = string.Empty;
    private string _patchSummary = string.Empty;
    private string _patchWarningDescription = string.Empty;
    private string _librarySummary = string.Empty;
    private string _searchText = string.Empty;
    private string _libraryMessage = string.Empty;
    private bool _isLibraryLoading;
    private bool _isLibraryLoaded;
    private bool _isChampionCatalogLoaded;
    private LocalCoachPackState _builtInPackState = LocalCoachPackState.Enabled;
    private CoachingScreen _currentScreen = CoachingScreen.Library;
    private bool _isDisposed;

    public CoachingViewModel(
        CoachPack pack,
        LocalizationService localization,
        IGamePatchCatalog gamePatchCatalog,
        IChampionCatalog championCatalog,
        IItemCatalog itemCatalog,
        SaveCoachPackUseCase saveCoachPack,
        LoadCoachPackLibraryUseCase loadCoachPackLibrary,
        SetCoachPackStateUseCase setCoachPackState)
    {
        _pack = pack;
        _localization = localization;
        _championCatalog = championCatalog ?? throw new ArgumentNullException(nameof(championCatalog));
        _loadCoachPackLibrary = loadCoachPackLibrary
            ?? throw new ArgumentNullException(nameof(loadCoachPackLibrary));
        _setCoachPackState = setCoachPackState
            ?? throw new ArgumentNullException(nameof(setCoachPackState));
        _localization.PropertyChanged += Localization_PropertyChanged;

        var firstRule = pack.Rules.FirstOrDefault();
        PlayerChampionInitials = GetInitials(firstRule?.PlayerChampion);
        OpponentChampionInitials = GetInitials(firstRule?.OpponentChampion);
        PackTypeSelection = new PackTypeSelectionViewModel();
        MatchupEditor = new MatchupEditorViewModel(
            localization,
            gamePatchCatalog,
            championCatalog,
            itemCatalog,
            saveCoachPack);
        RenderLocalizedPresentation();
    }

    public PackTypeSelectionViewModel PackTypeSelection { get; }

    public MatchupEditorViewModel MatchupEditor { get; }

    public CoachingScreen CurrentScreen
    {
        get => _currentScreen;
        private set => SetProperty(ref _currentScreen, value);
    }

    public string PackName => _pack.Name;

    public string PlayerChampionInitials { get; }

    public string OpponentChampionInitials { get; }

    public IReadOnlyList<CoachingRuleViewModel> Rules
    {
        get => _rules;
        private set => SetProperty(ref _rules, value);
    }

    public string RulesSummary
    {
        get => _rulesSummary;
        private set => SetProperty(ref _rulesSummary, value);
    }

    public string PatchSummary
    {
        get => _patchSummary;
        private set => SetProperty(ref _patchSummary, value);
    }

    public string PatchWarningDescription
    {
        get => _patchWarningDescription;
        private set => SetProperty(ref _patchWarningDescription, value);
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
                RenderLibrary();
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

    public void OpenPackTypeSelection() =>
        CurrentScreen = CoachingScreen.PackTypeSelection;

    public void OpenMatchupEditor() =>
        CurrentScreen = CoachingScreen.MatchupEditor;

    public void OpenLibrary() =>
        CurrentScreen = CoachingScreen.Library;

    public Task EnsureLibraryLoadedAsync(CancellationToken cancellationToken = default) =>
        _isLibraryLoaded
            ? Task.CompletedTask
            : RefreshLibraryAsync(cancellationToken);

    public async Task RefreshLibraryAsync(CancellationToken cancellationToken = default)
    {
        IsLibraryLoading = true;
        LibraryMessage = string.Empty;
        try
        {
            var snapshot = await _loadCoachPackLibrary.ExecuteAsync(cancellationToken);
            _localEntries = snapshot.Entries;
            _builtInPackState = snapshot.PackStates.GetValueOrDefault(
                _pack.Id,
                LocalCoachPackState.Enabled);
            _isLibraryLoaded = true;
            LibraryMessage = snapshot.InvalidFileCount > 0
                ? _localization.Plural("Count.CoachPackInvalid", snapshot.InvalidFileCount)
                : string.Empty;
            RenderLibrary();

            if (!_isChampionCatalogLoaded)
            {
                var catalog = await _championCatalog.LoadAsync(
                    _localization.CurrentLanguageCode,
                    cancellationToken);
                _champions = catalog.Champions.ToDictionary(
                    static champion => champion.Id,
                    StringComparer.OrdinalIgnoreCase);
                _isChampionCatalogLoaded = true;
                RenderLibrary();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException)
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
            await RefreshLibraryAsync(cancellationToken);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException)
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

    private void Localization_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        MatchupEditor.UpdateLanguage(_localization.CurrentLanguageCode);
        RenderLocalizedPresentation();
    }

    private void RenderLocalizedPresentation()
    {
        RulesSummary = _localization.Format("Coaching.Pack.Rules", _pack.Rules.Count);
        PatchSummary = _localization.Format("Coaching.Pack.Patch", _pack.Patch);
        PatchWarningDescription = _localization.Format(
            "Coaching.Update.WarningDescription",
            _pack.Patch);
        Rules = _pack.Rules
            .Select(CreateRuleViewModel)
            .ToArray();
        RenderLibrary();
    }

    private void RenderLibrary()
    {
        var cards = new List<CoachingPackCardViewModel>
        {
            CreatePackCard(_pack, CoachingPackOrigin.BuiltIn, _builtInPackState)
        };
        cards.AddRange(_localEntries.Select(entry => CreatePackCard(
            entry.Pack,
            CoachingPackOrigin.CreatedByUser,
            entry.State)));
        var filteredCards = cards
            .Where(MatchesSearch)
            .ToArray();

        MatchupCards = filteredCards;
        MyPackCards = filteredCards
            .Where(static card => card.IsCreatedByUser)
            .ToArray();
        LibrarySummary = string.Join(
            " · ",
            _localization.Plural("Count.CoachPack", cards.Count),
            _localization.Plural("Count.CoachPackEnabled", cards.Count(static card => card.IsEnabled)),
            _localization.Plural("Count.CoachPackCreated", cards.Count(static card => card.IsCreatedByUser)));
    }

    private CoachingPackCardViewModel CreatePackCard(
        CoachPack pack,
        CoachingPackOrigin origin,
        LocalCoachPackState state)
    {
        var firstRule = pack.Rules.FirstOrDefault();
        var playerChampionId = EmptyToFallback(pack.PlayerChampion, firstRule?.PlayerChampion);
        var opponentChampionId = EmptyToFallback(pack.OpponentChampion, firstRule?.OpponentChampion);
        _champions.TryGetValue(playerChampionId, out var playerChampion);
        _champions.TryGetValue(opponentChampionId, out var opponentChampion);
        var playerName = playerChampion?.DisplayName ?? playerChampionId;
        var opponentName = opponentChampion?.DisplayName ?? opponentChampionId;
        var author = string.IsNullOrWhiteSpace(pack.Author)
            ? _localization.Get("Coaching.Library.Author.TempoCue")
            : pack.Author;
        var role = string.IsNullOrWhiteSpace(pack.LaneRole)
            ? _localization.Get("Coaching.Library.Role.Any")
            : pack.LaneRole.ToUpperInvariant();
        var adviceCount = pack.Rules.Count + pack.PersistentTips.Count;

        return new CoachingPackCardViewModel(
            pack,
            origin,
            state,
            _localization.Format("Coaching.Library.Matchup", playerName, opponentName),
            GetInitials(playerName),
            GetInitials(opponentName),
            playerChampion?.ImageUri,
            opponentChampion?.ImageUri,
            _localization.Format("Coaching.Library.Card.Details", role, author),
            _localization.Plural("Count.Advice", adviceCount),
            _localization.Get(state switch
            {
                LocalCoachPackState.Enabled => "Coaching.Library.State.Enabled",
                LocalCoachPackState.Disabled => "Coaching.Library.State.Disabled",
                _ => "Coaching.Library.State.Draft"
            }),
            _localization.Get(origin == CoachingPackOrigin.BuiltIn
                ? "Coaching.Library.Origin.BuiltIn"
                : "Coaching.Library.Origin.Created"),
            GetContentLanguageLabel(pack.ContentLanguage),
            _localization.Format("Coaching.Library.Patch", pack.Patch));
    }

    private bool MatchesSearch(CoachingPackCardViewModel card)
    {
        var search = SearchText.Trim();
        return search.Length == 0
               || card.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
               || card.Details.Contains(search, StringComparison.CurrentCultureIgnoreCase)
               || card.Pack.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase);
    }

    private string GetContentLanguageLabel(string contentLanguage)
    {
        if (contentLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            return _localization.Get("Coaching.Library.Language.Russian");
        }

        if (contentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return _localization.Get("Coaching.Library.Language.English");
        }

        return string.IsNullOrWhiteSpace(contentLanguage)
            ? _localization.Get("Coaching.Library.Language.Unknown")
            : contentLanguage;
    }

    private static string EmptyToFallback(string value, string? fallback) =>
        string.IsNullOrWhiteSpace(value)
            ? fallback ?? "—"
            : value;

    private CoachingRuleViewModel CreateRuleViewModel(CoachRuleDefinition rule)
    {
        var powerStateKey = rule.PowerState switch
        {
            PowerState.Dangerous => "Coaching.Power.Dangerous",
            PowerState.Cautious => "Coaching.Power.Cautious",
            PowerState.Playable => "Coaching.Power.Playable",
            PowerState.Favorable => "Coaching.Power.Favorable",
            _ => throw new ArgumentOutOfRangeException(nameof(rule.PowerState))
        };
        var levelRange = rule.MaximumLevel is int maximumLevel
            ? _localization.Format(
                "Coaching.Rule.LevelRange",
                rule.MinimumLevel,
                maximumLevel)
            : _localization.Format("Coaching.Rule.LevelFrom", rule.MinimumLevel);

        return new CoachingRuleViewModel(
            rule.PowerState,
            _localization.Get(powerStateKey),
            levelRange,
            rule.Title,
            rule.Message,
            _localization.Format("Coaching.Rule.Source", rule.Source));
    }

    private static string GetInitials(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Take(3)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }
}
