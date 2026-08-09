using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Метаданные и состояние готовности первого шага редактора матчапа.</summary>
public sealed class MatchupMainInformationViewModel : ObservableObject
{
    private string _packName = string.Empty;
    private string _shortDescription = string.Empty;
    private string _author = string.Empty;
    private string _adviceLanguageCode;
    private string _gamePatch = string.Empty;
    private MatchupSourceKind _selectedSourceKind = MatchupSourceKind.PersonalExperience;
    private string _sourceUrl = string.Empty;

    public MatchupMainInformationViewModel(string defaultLanguageCode)
    {
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

    public string GamePatch
    {
        get => _gamePatch;
        set
        {
            if (SetProperty(ref _gamePatch, value ?? string.Empty))
            {
                NotifyFormStateChanged();
            }
        }
    }

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
        && IsWithinLength(GamePatch, 2, 24)
        && AdviceLanguages.Any(language => language.Code == AdviceLanguageCode)
        && IsSourceReady;

    private void NotifyFormStateChanged()
    {
        OnPropertyChanged(nameof(IsSourceUrlRequired));
        OnPropertyChanged(nameof(IsSourceReady));
        OnPropertyChanged(nameof(IsComplete));
    }

    private static bool IsWithinLength(string value, int minimumLength, int maximumLength)
    {
        var length = value.Trim().Length;
        return length >= minimumLength && length <= maximumLength;
    }

    private static bool IsValidWebUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
