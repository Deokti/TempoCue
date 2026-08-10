using System.Globalization;
using System.IO;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Финальная проверка метаданных и локальное сохранение созданного матчапа.</summary>
public sealed class MatchupSaveViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly SaveCoachPackUseCase _saveCoachPack;
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];
    private MatchupMainInformationViewModel? _mainInformation;
    private MatchupChampionsAndRoleViewModel? _championsAndRole;
    private MatchupContentViewModel? _content;
    private MatchupSourceKind _selectedSourceKind = MatchupSourceKind.PersonalExperience;
    private string _sourceTitle = string.Empty;
    private string _sourceUrl = string.Empty;
    private string _sourceAuthor = string.Empty;
    private bool _authorPermissionGranted;
    private bool _isSaving;
    private string _saveStatus = string.Empty;
    private string _savedPath = string.Empty;
    private bool _hasSaveError;
    private LocalCoachPackState _savedState = LocalCoachPackState.Draft;

    public MatchupSaveViewModel(
        LocalizationService localization,
        SaveCoachPackUseCase saveCoachPack)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _saveCoachPack = saveCoachPack ?? throw new ArgumentNullException(nameof(saveCoachPack));
    }

    public MatchupSourceKind SelectedSourceKind
    {
        get => _selectedSourceKind;
        set
        {
            if (SetProperty(ref _selectedSourceKind, value))
            {
                if (value == MatchupSourceKind.PersonalExperience)
                {
                    AuthorPermissionGranted = false;
                }

                NotifySourceChanged();
            }
        }
    }

    public bool IsPersonalExperience => SelectedSourceKind == MatchupSourceKind.PersonalExperience;

    public bool IsArticle => SelectedSourceKind == MatchupSourceKind.Article;

    public bool IsVideo => SelectedSourceKind == MatchupSourceKind.Video;

    public bool IsGuide => SelectedSourceKind == MatchupSourceKind.Guide;

    public bool HasExternalSource => !IsPersonalExperience;

    public string SourceTitle
    {
        get => _sourceTitle;
        set
        {
            if (SetProperty(ref _sourceTitle, value ?? string.Empty))
            {
                NotifySourceChanged();
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
                NotifySourceChanged();
            }
        }
    }

    public string SourceAuthor
    {
        get => _sourceAuthor;
        set
        {
            if (SetProperty(ref _sourceAuthor, value ?? string.Empty))
            {
                NotifySourceChanged();
            }
        }
    }

    public bool AuthorPermissionGranted
    {
        get => _authorPermissionGranted;
        set
        {
            if (SetProperty(ref _authorPermissionGranted, value))
            {
                OnPropertyChanged(nameof(PermissionLabel));
                OnPropertyChanged(nameof(SourceSummary));
            }
        }
    }

    public bool IsSaving
    {
        get => _isSaving;
        private set
        {
            if (SetProperty(ref _isSaving, value))
            {
                OnPropertyChanged(nameof(CanSave));
            }
        }
    }

    public string SaveStatus
    {
        get => _saveStatus;
        private set => SetProperty(ref _saveStatus, value);
    }

    public string SavedPath
    {
        get => _savedPath;
        private set
        {
            if (SetProperty(ref _savedPath, value))
            {
                OnPropertyChanged(nameof(HasSavedPath));
            }
        }
    }

    public bool HasSavedPath => !string.IsNullOrWhiteSpace(SavedPath);

    public bool HasSaveError
    {
        get => _hasSaveError;
        private set => SetProperty(ref _hasSaveError, value);
    }

    public string PackStateLabel => _localization.Get(_savedState == LocalCoachPackState.Enabled
        ? "Coaching.MatchupEditor.Save.Card.State.Enabled"
        : "Coaching.MatchupEditor.Draft");

    public bool IsSourceComplete => IsPersonalExperience
        || TextLengthFeedback.IsWithin(SourceTitle, 3, 160)
        && TextLengthFeedback.IsWithin(SourceAuthor, 2, 80)
        && IsHttpUrl(SourceUrl);

    public bool CanSave => !IsSaving
        && IsSourceComplete
        && _mainInformation is not null
        && _championsAndRole is not null
        && _content is not null;

    public string SourceValidationMessage => IsSourceComplete
        ? string.Empty
        : _localization.Get("Coaching.MatchupEditor.Save.Source.Validation");

    public string PhaseCountTitle => _localization.Plural(
        "Count.PhaseCompleted",
        _content?.PhaseCount ?? 0);

    public string PhaseValidationDescription => _content?.Validation.State == MatchupPhaseValidationState.Warning
        ? _localization.Get("Coaching.MatchupEditor.Save.Check.Phases.Warning")
        : _localization.Get("Coaching.MatchupEditor.Save.Check.Phases.Ready");

    public string PatchTitle => _localization.Format(
        "Coaching.MatchupEditor.Save.Check.Patch",
        _mainInformation?.GamePatch ?? string.Empty);

    public string PermissionTitle => IsPersonalExperience || AuthorPermissionGranted
        ? _localization.Get("Coaching.MatchupEditor.Save.Check.Permission.Ready")
        : _localization.Get("Coaching.MatchupEditor.Save.Check.Permission.Missing");

    public string PermissionDescription => IsPersonalExperience
        ? _localization.Get("Coaching.MatchupEditor.Save.Check.Permission.Personal")
        : AuthorPermissionGranted
            ? _localization.Get("Coaching.MatchupEditor.Save.Check.Permission.Granted")
            : _localization.Get("Coaching.MatchupEditor.Save.Check.Permission.LocalOnly");

    public string PermissionLabel => AuthorPermissionGranted
        ? _localization.Get("Coaching.MatchupEditor.Save.Permission.Granted")
        : _localization.Get("Coaching.MatchupEditor.Save.Permission.NotConfirmed");

    public string PackMatchupSummary => _localization.Format(
        "Coaching.MatchupEditor.Save.Card.Matchup",
        _championsAndRole?.SelectedPlayerChampion?.DisplayName ?? "—",
        _championsAndRole?.SelectedOpponentChampion?.DisplayName ?? "—",
        GetRoleLabel());

    public string PackAuthor => _mainInformation?.Author ?? "—";

    public string PackLanguage => _localization.Format(
        "Coaching.MatchupEditor.Save.Card.Language",
        GetContentLanguageLabel());

    public string PackPatch => _mainInformation?.GamePatch ?? "—";

    public string SourceSummary => IsPersonalExperience
        ? _localization.Get("Coaching.MatchupEditor.Save.Source.PersonalExperience")
        : _localization.Format(
            "Coaching.MatchupEditor.Save.Card.Source",
            GetSourceKindLabel(),
            PermissionLabel);

    public string VerifiedOn => _mainInformation?.VerifiedOn.ToString(
        "d MMMM yyyy",
        CultureInfo.GetCultureInfo(_localization.CurrentLanguageCode)) ?? "—";

    public void Update(
        MatchupMainInformationViewModel mainInformation,
        MatchupChampionsAndRoleViewModel championsAndRole,
        MatchupContentViewModel content)
    {
        _mainInformation = mainInformation ?? throw new ArgumentNullException(nameof(mainInformation));
        _championsAndRole = championsAndRole ?? throw new ArgumentNullException(nameof(championsAndRole));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        NotifyDerivedStateChanged();
    }

    public async Task<bool> SaveAsync(
        LocalCoachPackState state,
        CancellationToken cancellationToken = default)
    {
        if (!CanSave)
        {
            return false;
        }

        IsSaving = true;
        HasSaveError = false;
        SaveStatus = _localization.Get("Coaching.MatchupEditor.Save.Status.Saving");
        SavedPath = string.Empty;
        try
        {
            var result = await _saveCoachPack.ExecuteAsync(
                CreateCoachPack(),
                state,
                cancellationToken);
            SavedPath = result.DisplayPath;
            _savedState = result.State;
            OnPropertyChanged(nameof(PackStateLabel));
            SaveStatus = _localization.Get(state == LocalCoachPackState.Draft
                ? "Coaching.MatchupEditor.Save.Status.DraftSaved"
                : "Coaching.MatchupEditor.Save.Status.EnabledSaved");
            return true;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException)
        {
            HasSaveError = true;
            SaveStatus = _localization.Get("Coaching.MatchupEditor.Save.Status.Failed");
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public void UpdateLanguage()
    {
        NotifyDerivedStateChanged();
        if (HasSavedPath)
        {
            SaveStatus = _localization.Get("Coaching.MatchupEditor.Save.Status.Saved");
        }
    }

    private CoachPack CreateCoachPack()
    {
        var mainInformation = _mainInformation
            ?? throw new InvalidOperationException("Основная информация не подготовлена.");
        var champions = _championsAndRole
            ?? throw new InvalidOperationException("Чемпионы не подготовлены.");
        var content = _content
            ?? throw new InvalidOperationException("Содержание матчапа не подготовлено.");
        var playerChampion = champions.SelectedPlayerChampion
            ?? throw new InvalidOperationException("Чемпион игрока не выбран.");
        var opponentChampion = champions.SelectedOpponentChampion
            ?? throw new InvalidOperationException("Чемпион противника не выбран.");
        var source = CreateSource();
        var sourceSummary = CreateRuleSource(source, mainInformation.AdviceLanguageCode);

        return new CoachPack
        {
            SchemaVersion = 2,
            Id = $"matchup.{NormalizeId(playerChampion.Id)}-vs-{NormalizeId(opponentChampion.Id)}.{_instanceId}",
            Name = mainInformation.PackName.Trim(),
            Patch = mainInformation.GamePatch,
            Description = mainInformation.ShortDescription.Trim(),
            Author = mainInformation.Author.Trim(),
            ContentLanguage = mainInformation.AdviceLanguageCode,
            PlayerChampion = playerChampion.Id,
            OpponentChampion = opponentChampion.Id,
            LaneRole = champions.SelectedRole.ToString().ToLowerInvariant(),
            VerifiedOn = DateOnly.FromDateTime(mainInformation.VerifiedOn),
            Source = source,
            Rules = content.Phases.Select((phase, index) => new CoachRuleDefinition
            {
                Id = phase.Id,
                PlayerChampion = playerChampion.Id,
                OpponentChampion = opponentChampion.Id,
                MinimumLevel = phase.MinimumLevel,
                MaximumLevel = phase.MaximumLevel,
                PowerState = phase.PowerState,
                Priority = 1000 - index,
                Title = phase.Name,
                Message = phase.PrimaryAdvice,
                WhyNow = phase.WhyNow,
                NextGoal = phase.NextGoal,
                RecommendedItemIds = phase.RecommendedItems.Select(static item => item.Id).ToArray(),
                Source = sourceSummary
            }).ToArray(),
            PersistentTips = content.PersistentTips.Tips.Select(tip => new CoachPersistentTipDefinition
            {
                Id = tip.Id,
                Title = tip.Title,
                Message = tip.Text,
                ApplicableRuleId = tip.ApplicablePhaseId,
                Source = tip.CustomSource
            }).ToArray(),
            ItemBlocks = content.ItemBlocks.Blocks.Select(block => new CoachItemBlockDefinition
            {
                Id = block.Id,
                Name = block.Name,
                WhenToChoose = block.WhenToChoose,
                ItemIds = block.Items.Select(static item => item.Id).ToArray()
            }).ToArray()
        };
    }

    private CoachPackSource CreateSource() => new()
    {
        Kind = SelectedSourceKind switch
        {
            MatchupSourceKind.PersonalExperience => CoachPackSourceKind.PersonalExperience,
            MatchupSourceKind.Article => CoachPackSourceKind.Article,
            MatchupSourceKind.Video => CoachPackSourceKind.Video,
            MatchupSourceKind.Guide => CoachPackSourceKind.CommunityGuide,
            _ => throw new ArgumentOutOfRangeException()
        },
        Title = SourceTitle.Trim(),
        Url = SourceUrl.Trim(),
        Author = SourceAuthor.Trim(),
        Permission = IsPersonalExperience
            ? CoachPackSourcePermission.NotRequired
            : AuthorPermissionGranted
                ? CoachPackSourcePermission.Granted
                : CoachPackSourcePermission.NotConfirmed
    };

    private static string CreateRuleSource(CoachPackSource source, string languageCode)
    {
        if (source.Kind == CoachPackSourceKind.PersonalExperience)
        {
            return languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
                ? "Личный опыт автора"
                : "Author's personal experience";
        }

        return string.IsNullOrWhiteSpace(source.Author)
            ? source.Title
            : $"{source.Title} — {source.Author}";
    }

    private void NotifySourceChanged()
    {
        OnPropertyChanged(nameof(IsPersonalExperience));
        OnPropertyChanged(nameof(IsArticle));
        OnPropertyChanged(nameof(IsVideo));
        OnPropertyChanged(nameof(IsGuide));
        OnPropertyChanged(nameof(HasExternalSource));
        OnPropertyChanged(nameof(IsSourceComplete));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SourceValidationMessage));
        OnPropertyChanged(nameof(PermissionTitle));
        OnPropertyChanged(nameof(PermissionDescription));
        OnPropertyChanged(nameof(SourceSummary));
    }

    private void NotifyDerivedStateChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(PhaseCountTitle));
        OnPropertyChanged(nameof(PhaseValidationDescription));
        OnPropertyChanged(nameof(PatchTitle));
        OnPropertyChanged(nameof(PermissionTitle));
        OnPropertyChanged(nameof(PermissionDescription));
        OnPropertyChanged(nameof(PermissionLabel));
        OnPropertyChanged(nameof(PackMatchupSummary));
        OnPropertyChanged(nameof(PackAuthor));
        OnPropertyChanged(nameof(PackLanguage));
        OnPropertyChanged(nameof(PackPatch));
        OnPropertyChanged(nameof(SourceSummary));
        OnPropertyChanged(nameof(VerifiedOn));
        OnPropertyChanged(nameof(PackStateLabel));
        OnPropertyChanged(nameof(SourceValidationMessage));
    }

    private string GetRoleLabel() => _championsAndRole?.SelectedRole switch
    {
        MatchupLaneRole.Top => "TOP",
        MatchupLaneRole.Jungle => "JUNGLE",
        MatchupLaneRole.Mid => "MID",
        MatchupLaneRole.Adc => "ADC",
        MatchupLaneRole.Support => "SUPPORT",
        _ => _localization.Get("Coaching.MatchupEditor.Champions.Role.Any")
    };

    private string GetContentLanguageLabel() => _mainInformation?.AdviceLanguageCode.StartsWith(
        "ru",
        StringComparison.OrdinalIgnoreCase) == true
        ? "Русский"
        : "English";

    private string GetSourceKindLabel() => _localization.Get(SelectedSourceKind switch
    {
        MatchupSourceKind.PersonalExperience => "Coaching.MatchupEditor.Save.Source.PersonalExperience",
        MatchupSourceKind.Article => "Coaching.MatchupEditor.Save.Source.Article",
        MatchupSourceKind.Video => "Coaching.MatchupEditor.Save.Source.Video",
        MatchupSourceKind.Guide => "Coaching.MatchupEditor.Save.Source.Guide",
        _ => throw new ArgumentOutOfRangeException()
    });

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";

    private static string NormalizeId(string value) => new(
        value
            .Where(static character => char.IsLetterOrDigit(character))
            .Select(static character => char.ToLowerInvariant(character))
            .ToArray());
}
