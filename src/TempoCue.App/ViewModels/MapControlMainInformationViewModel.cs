using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Application;

namespace TempoCue.App.ViewModels;

/// <summary>Основные метаданные нового набора контроля карты.</summary>
public sealed class MapControlMainInformationViewModel : ObservableObject
{
    private const int MinimumNameLength = 4;
    private const int MaximumNameLength = 60;
    private const int MinimumDescriptionLength = 10;
    private const int MaximumDescriptionLength = 160;

    private readonly LocalizationService _localization;
    private string _packName = string.Empty;
    private string _shortDescription = string.Empty;
    private string _contentLanguageCode;

    public MapControlMainInformationViewModel(
        string defaultLanguageCode,
        IGamePatchCatalog gamePatchCatalog,
        LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _contentLanguageCode = LocalizationService.NormalizeLanguageCode(defaultLanguageCode);
        PatchSelection = new GamePatchSelectionViewModel(gamePatchCatalog, localization);
        PatchSelection.PropertyChanged += PatchSelection_PropertyChanged;
    }

    public IReadOnlyList<AdviceLanguageOptionViewModel> ContentLanguages { get; } =
    [
        new("ru-RU", "Русский"),
        new("en-US", "English")
    ];

    public GamePatchSelectionViewModel PatchSelection { get; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(PackName)
        ? _localization.Get("Coaching.MapControlEditor.Title")
        : PackName.Trim();

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

    public string ContentLanguageCode
    {
        get => _contentLanguageCode;
        set
        {
            var normalizedCode = LocalizationService.NormalizeLanguageCode(value);
            if (SetProperty(ref _contentLanguageCode, normalizedCode))
            {
                NotifyFormStateChanged();
            }
        }
    }

    public string GamePatch => PatchSelection.Patch;

    public TextLengthFeedback PackNameLengthFeedback => TextLengthFeedback.Create(
        PackName,
        MinimumNameLength,
        MaximumNameLength,
        _localization);

    public TextLengthFeedback ShortDescriptionLengthFeedback => TextLengthFeedback.Create(
        ShortDescription,
        MinimumDescriptionLength,
        MaximumDescriptionLength,
        _localization);

    public bool IsComplete =>
        TextLengthFeedback.IsWithin(PackName, MinimumNameLength, MaximumNameLength)
        && TextLengthFeedback.IsWithin(
            ShortDescription,
            MinimumDescriptionLength,
            MaximumDescriptionLength)
        && ContentLanguages.Any(language => language.Code == ContentLanguageCode)
        && PatchSelection.IsComplete;

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
        ContentLanguageCode = _localization.CurrentLanguageCode;
        PatchSelection.ResetToCurrent();
    }

    public void UpdateLanguage()
    {
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(PackNameLengthFeedback));
        OnPropertyChanged(nameof(ShortDescriptionLengthFeedback));
        PatchSelection.UpdateLanguage();
    }

    private void PatchSelection_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(GamePatchSelectionViewModel.IsComplete)
            or nameof(GamePatchSelectionViewModel.SelectedOption)
            or nameof(GamePatchSelectionViewModel.State))
        {
            OnPropertyChanged(nameof(GamePatch));
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
