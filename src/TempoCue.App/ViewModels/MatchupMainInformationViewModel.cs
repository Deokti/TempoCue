using System.ComponentModel;
using System.Globalization;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Метаданные и состояние готовности первого шага редактора матчапа.</summary>
public sealed class MatchupMainInformationViewModel : ObservableObject
{
    private const int MaxPackNameLength = 80;

    private readonly LocalizationService _localization;
    private string _packName = string.Empty;
    private string _shortDescription = string.Empty;
    private string _author = string.Empty;
    private string _adviceLanguageCode;
    private DateTime _verifiedOn = DateTime.Today;

    public MatchupMainInformationViewModel(
        string defaultLanguageCode,
        IGamePatchCatalog gamePatchCatalog,
        LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(gamePatchCatalog);
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _adviceLanguageCode = LocalizationService.NormalizeLanguageCode(defaultLanguageCode);
        PatchSelection = new GamePatchSelectionViewModel(gamePatchCatalog, localization);
        PatchSelection.PropertyChanged += PatchSelection_PropertyChanged;
    }

    public IReadOnlyList<AdviceLanguageOptionViewModel> AdviceLanguages { get; } =
    [
        new("ru-RU", "Русский"),
        new("en-US", "English")
    ];

    public GamePatchSelectionViewModel PatchSelection { get; }

    public DateTime VerifiedOn
    {
        get => _verifiedOn;
        private set
        {
            if (SetProperty(ref _verifiedOn, value))
            {
                OnPropertyChanged(nameof(VerifiedOnDisplayName));
            }
        }
    }

    public string DisplayTitle => string.IsNullOrWhiteSpace(PackName)
        ? _localization.Get("Coaching.MatchupEditor.Title")
        : PackName.Trim();

    public string VerifiedOnDisplayName => VerifiedOn.ToString(
        "d MMMM yyyy",
        CultureInfo.GetCultureInfo(_localization.CurrentLanguageCode));

    public string PackName
    {
        get => _packName;
        set
        {
            if (SetProperty(ref _packName, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(DisplayTitle));
                NotifyFormStateChanged(nameof(PackNameLengthFeedback));
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
                NotifyFormStateChanged(nameof(ShortDescriptionLengthFeedback));
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
                NotifyFormStateChanged(nameof(AuthorLengthFeedback));
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

    public IReadOnlyList<GamePatchOptionViewModel> GamePatches => PatchSelection.Options;

    public GamePatchOptionViewModel? SelectedGamePatch
    {
        get => PatchSelection.SelectedOption;
        set => PatchSelection.SelectedOption = value;
    }

    public string GamePatch => PatchSelection.Patch;

    public TextLengthFeedback PackNameLengthFeedback => TextLengthFeedback.Create(
        PackName,
        3,
        MaxPackNameLength,
        _localization);

    public TextLengthFeedback ShortDescriptionLengthFeedback => TextLengthFeedback.Create(
        ShortDescription,
        10,
        240,
        _localization);

    public TextLengthFeedback AuthorLengthFeedback => TextLengthFeedback.Create(
        Author,
        2,
        40,
        _localization);

    public CatalogViewState GamePatchCatalogState => PatchSelection.State;

    public bool IsGamePatchCatalogReady => PatchSelection.IsReady;

    public bool IsGamePatchCatalogLoading => PatchSelection.IsLoading;

    public bool HasGamePatchCatalogError => PatchSelection.HasError;

    public bool IsUsingGamePatchCache => PatchSelection.IsUsingCache;

    public bool IsComplete =>
        TextLengthFeedback.IsWithin(PackName, 3, MaxPackNameLength)
        && TextLengthFeedback.IsWithin(ShortDescription, 10, 240)
        && TextLengthFeedback.IsWithin(Author, 2, 40)
        && PatchSelection.IsComplete
        && AdviceLanguages.Any(language => language.Code == AdviceLanguageCode);

    public Task EnsureGamePatchCatalogLoadedAsync(
        CancellationToken cancellationToken = default) =>
        PatchSelection.EnsureLoadedAsync(cancellationToken);

    public Task RetryGamePatchCatalogLoadAsync(
        CancellationToken cancellationToken = default) =>
        PatchSelection.RetryLoadAsync(cancellationToken);

    public void BeginNew()
    {
        PackName = string.Empty;
        ShortDescription = string.Empty;
        Author = string.Empty;
        AdviceLanguageCode = _localization.CurrentLanguageCode;
        VerifiedOn = DateTime.Today;
        PatchSelection.ResetToCurrent();
    }

    public void Load(CoachPack pack, bool createCopy)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var packName = createCopy
            ? _localization.Format("Coaching.MatchupEditor.CopyName", pack.Name)
            : pack.Name;
        PackName = packName.Length <= MaxPackNameLength
            ? packName
            : packName[..MaxPackNameLength].TrimEnd();
        ShortDescription = pack.Description;
        Author = createCopy ? string.Empty : pack.Author;
        AdviceLanguageCode = string.IsNullOrWhiteSpace(pack.ContentLanguage)
            ? _localization.CurrentLanguageCode
            : pack.ContentLanguage;
        VerifiedOn = createCopy
            ? DateTime.Today
            : pack.VerifiedOn?.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        PatchSelection.SelectPatch(pack.Patch);
    }

    public void UpdateLanguage()
    {
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(VerifiedOnDisplayName));
        OnPropertyChanged(nameof(PackNameLengthFeedback));
        OnPropertyChanged(nameof(ShortDescriptionLengthFeedback));
        OnPropertyChanged(nameof(AuthorLengthFeedback));
        PatchSelection.UpdateLanguage();
    }

    private void PatchSelection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(GamePatchSelectionViewModel.Options):
                OnPropertyChanged(nameof(GamePatches));
                break;
            case nameof(GamePatchSelectionViewModel.SelectedOption):
                OnPropertyChanged(nameof(SelectedGamePatch));
                break;
            case nameof(GamePatchSelectionViewModel.Patch):
                OnPropertyChanged(nameof(GamePatch));
                break;
            case nameof(GamePatchSelectionViewModel.State):
                OnPropertyChanged(nameof(GamePatchCatalogState));
                break;
            case nameof(GamePatchSelectionViewModel.IsReady):
                OnPropertyChanged(nameof(IsGamePatchCatalogReady));
                break;
            case nameof(GamePatchSelectionViewModel.IsLoading):
                OnPropertyChanged(nameof(IsGamePatchCatalogLoading));
                break;
            case nameof(GamePatchSelectionViewModel.HasError):
                OnPropertyChanged(nameof(HasGamePatchCatalogError));
                break;
            case nameof(GamePatchSelectionViewModel.IsUsingCache):
                OnPropertyChanged(nameof(IsUsingGamePatchCache));
                break;
        }

        if (e.PropertyName is nameof(GamePatchSelectionViewModel.IsComplete)
            or nameof(GamePatchSelectionViewModel.SelectedOption)
            or nameof(GamePatchSelectionViewModel.State))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private void NotifyFormStateChanged(string? feedbackPropertyName = null)
    {
        OnPropertyChanged(nameof(IsComplete));
        if (feedbackPropertyName is not null)
        {
            OnPropertyChanged(feedbackPropertyName);
        }
    }
}
