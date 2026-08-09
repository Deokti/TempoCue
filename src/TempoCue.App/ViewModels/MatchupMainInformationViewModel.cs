using TempoCue.App.Localization;
using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>Метаданные и состояние готовности первого шага редактора матчапа.</summary>
public sealed class MatchupMainInformationViewModel : ObservableObject
{
    private readonly IGamePatchCatalog _gamePatchCatalog;
    private readonly LocalizationService _localization;
    private string _packName = string.Empty;
    private string _shortDescription = string.Empty;
    private string _author = string.Empty;
    private string _adviceLanguageCode;
    private IReadOnlyList<GamePatchOptionViewModel> _gamePatches = [];
    private GamePatchOptionViewModel? _selectedGamePatch;
    private CatalogViewState _gamePatchCatalogState = CatalogViewState.NotLoaded;
    private MatchupSourceKind _selectedSourceKind = MatchupSourceKind.PersonalExperience;
    private string _sourceUrl = string.Empty;

    public MatchupMainInformationViewModel(
        string defaultLanguageCode,
        IGamePatchCatalog gamePatchCatalog,
        LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(gamePatchCatalog);
        ArgumentNullException.ThrowIfNull(localization);

        _gamePatchCatalog = gamePatchCatalog;
        _localization = localization;
        _adviceLanguageCode = LocalizationService.NormalizeLanguageCode(defaultLanguageCode);
    }

    public IReadOnlyList<AdviceLanguageOptionViewModel> AdviceLanguages { get; } =
    [
        new("ru-RU", "Русский"),
        new("en-US", "English")
    ];

    public DateTime VerifiedOn { get; } = DateTime.Today;

    public string PackName
    {
        get => _packName;
        set
        {
            if (SetProperty(ref _packName, value ?? string.Empty))
            {
                NotifyFormStateChanged();
            }
        }
    }

    public string ShortDescription
    {
        get => _shortDescription;
        set
        {
            if (SetProperty(ref _shortDescription, value ?? string.Empty))
            {
                NotifyFormStateChanged();
            }
        }
    }

    public string Author
    {
        get => _author;
        set
        {
            if (SetProperty(ref _author, value ?? string.Empty))
            {
                NotifyFormStateChanged();
            }
        }
    }

    public string AdviceLanguageCode
    {
        get => _adviceLanguageCode;
        set
        {
            var normalizedCode = LocalizationService.NormalizeLanguageCode(value);
            if (SetProperty(ref _adviceLanguageCode, normalizedCode))
            {
                NotifyFormStateChanged();
            }
        }
    }

    public IReadOnlyList<GamePatchOptionViewModel> GamePatches => _gamePatches;

    public GamePatchOptionViewModel? SelectedGamePatch
    {
        get => _selectedGamePatch;
        set
        {
            if (SetProperty(ref _selectedGamePatch, value))
            {
                OnPropertyChanged(nameof(GamePatch));
                NotifyFormStateChanged();
            }
        }
    }

    public string GamePatch => SelectedGamePatch?.Patch ?? string.Empty;

    public CatalogViewState GamePatchCatalogState
    {
        get => _gamePatchCatalogState;
        private set
        {
            if (SetProperty(ref _gamePatchCatalogState, value))
            {
                OnPropertyChanged(nameof(IsGamePatchCatalogReady));
                OnPropertyChanged(nameof(IsGamePatchCatalogLoading));
                OnPropertyChanged(nameof(HasGamePatchCatalogError));
                OnPropertyChanged(nameof(IsUsingGamePatchCache));
                OnPropertyChanged(nameof(IsComplete));
            }
        }
    }

    public bool IsGamePatchCatalogReady =>
        GamePatchCatalogState is CatalogViewState.Ready or CatalogViewState.Cached;

    public bool IsGamePatchCatalogLoading =>
        GamePatchCatalogState == CatalogViewState.Loading;

    public bool HasGamePatchCatalogError =>
        GamePatchCatalogState == CatalogViewState.Failed;

    public bool IsUsingGamePatchCache =>
        GamePatchCatalogState == CatalogViewState.Cached;

    public MatchupSourceKind SelectedSourceKind
    {
        get => _selectedSourceKind;
        set
        {
            if (SetProperty(ref _selectedSourceKind, value))
            {
                NotifyFormStateChanged();
            }
        }
    }

    public string SourceUrl
    {
        get => _sourceUrl;
        set
        {
            if (SetProperty(ref _sourceUrl, value ?? string.Empty))
            {
                NotifyFormStateChanged();
            }
        }
    }

    public bool IsSourceUrlRequired =>
        SelectedSourceKind is not MatchupSourceKind.PersonalExperience;

    public bool IsSourceReady =>
        !IsSourceUrlRequired || IsValidWebUrl(SourceUrl);

    public bool IsComplete =>
        IsWithinLength(PackName, 3, 80)
        && IsWithinLength(ShortDescription, 10, 240)
        && IsWithinLength(Author, 2, 40)
        && IsGamePatchCatalogReady
        && SelectedGamePatch is not null
        && AdviceLanguages.Any(language => language.Code == AdviceLanguageCode)
        && IsSourceReady;

    public async Task EnsureGamePatchCatalogLoadedAsync(
        CancellationToken cancellationToken = default)
    {
        if (IsGamePatchCatalogLoading || IsGamePatchCatalogReady)
        {
            return;
        }

        GamePatchCatalogState = CatalogViewState.Loading;
        var previouslySelectedPatch = SelectedGamePatch?.Patch;
        var result = await _gamePatchCatalog.LoadAsync(cancellationToken);
        if (!result.IsAvailable)
        {
            _gamePatches = [];
            SelectedGamePatch = null;
            OnPropertyChanged(nameof(GamePatches));
            GamePatchCatalogState = CatalogViewState.Failed;
            return;
        }

        _gamePatches = CreatePatchOptions(result.Patches);
        OnPropertyChanged(nameof(GamePatches));
        SelectedGamePatch = FindPatch(previouslySelectedPatch)
            ?? _gamePatches.FirstOrDefault(static patch => patch.IsCurrent)
            ?? _gamePatches.FirstOrDefault();
        GamePatchCatalogState = result.IsFromCache
            ? CatalogViewState.Cached
            : CatalogViewState.Ready;
    }

    public Task RetryGamePatchCatalogLoadAsync(
        CancellationToken cancellationToken = default)
    {
        GamePatchCatalogState = CatalogViewState.NotLoaded;
        return EnsureGamePatchCatalogLoadedAsync(cancellationToken);
    }

    public void UpdateLanguage()
    {
        if (_gamePatches.Count == 0)
        {
            return;
        }

        var selectedPatch = SelectedGamePatch?.Patch;
        _gamePatches = _gamePatches
            .Select(patch => CreatePatchOption(patch.Patch, patch.IsCurrent))
            .ToArray();
        OnPropertyChanged(nameof(GamePatches));
        SelectedGamePatch = FindPatch(selectedPatch);
    }

    private void NotifyFormStateChanged()
    {
        OnPropertyChanged(nameof(IsSourceUrlRequired));
        OnPropertyChanged(nameof(IsSourceReady));
        OnPropertyChanged(nameof(IsComplete));
    }

    private IReadOnlyList<GamePatchOptionViewModel> CreatePatchOptions(
        IReadOnlyList<GamePatchCatalogEntry> patches) =>
        patches
            .Select(patch => CreatePatchOption(patch.Patch, patch.IsCurrent))
            .ToArray();

    private GamePatchOptionViewModel CreatePatchOption(string patch, bool isCurrent) => new(
        patch,
        isCurrent
            ? _localization.Format("Coaching.MatchupEditor.Main.GamePatch.Current", patch)
            : patch,
        isCurrent);

    private GamePatchOptionViewModel? FindPatch(string? patch) =>
        patch is null
            ? null
            : _gamePatches.FirstOrDefault(option =>
                string.Equals(option.Patch, patch, StringComparison.OrdinalIgnoreCase));

    private static bool IsWithinLength(string value, int minimumLength, int maximumLength)
    {
        var length = value.Trim().Length;
        return length >= minimumLength && length <= maximumLength;
    }

    private static bool IsValidWebUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
