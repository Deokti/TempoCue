using System.IO;
using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Подготавливает итоговую карточку и сохраняет Map Control Pack через Application.</summary>
public sealed class MapControlSaveViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly SaveMapControlPackUseCase _saveMapControlPack;
    private readonly MapControlPackFactory _packFactory = new();
    private MapControlMainInformationViewModel? _mainInformation;
    private MapControlScopeViewModel? _scope;
    private MapControlContentViewModel? _content;
    private CoachPackSourceKind _selectedSourceKind = CoachPackSourceKind.PersonalExperience;
    private string _sourceTitle = string.Empty;
    private string _sourceAuthor = string.Empty;
    private string _sourceUrl = string.Empty;
    private string? _targetPackId;
    private bool _isSaving;
    private bool _hasSaveError;
    private string _saveStatus = string.Empty;

    public MapControlSaveViewModel(
        LocalizationService localization,
        SaveMapControlPackUseCase saveMapControlPack)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _saveMapControlPack = saveMapControlPack
            ?? throw new ArgumentNullException(nameof(saveMapControlPack));
    }

    public CoachPackSourceKind SelectedSourceKind
    {
        get => _selectedSourceKind;
        set
        {
            if (SetProperty(ref _selectedSourceKind, value))
            {
                NotifySourceChanged();
            }
        }
    }

    public bool IsPersonalExperience => SelectedSourceKind == CoachPackSourceKind.PersonalExperience;

    public bool IsArticle => SelectedSourceKind == CoachPackSourceKind.Article;

    public bool IsVideo => SelectedSourceKind == CoachPackSourceKind.Video;

    public bool IsTrainingMaterial => SelectedSourceKind == CoachPackSourceKind.CommunityGuide;

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

    public bool HasSaveError
    {
        get => _hasSaveError;
        private set => SetProperty(ref _hasSaveError, value);
    }

    public string SaveStatus
    {
        get => _saveStatus;
        private set
        {
            if (SetProperty(ref _saveStatus, value))
            {
                OnPropertyChanged(nameof(HasSaveStatus));
            }
        }
    }

    public bool HasSaveStatus => !string.IsNullOrWhiteSpace(SaveStatus);

    public bool IsSourceComplete => IsPersonalExperience
        || TextLengthFeedback.IsWithin(SourceTitle, 3, 160)
        && TextLengthFeedback.IsWithin(SourceAuthor, 2, 80)
        && IsOptionalHttpUrl(SourceUrl);

    public bool CanSave => !IsSaving
        && IsSourceComplete
        && _mainInformation is not null
        && _scope is not null
        && _content is not null;

    public string SourceValidationMessage => IsSourceComplete
        ? string.Empty
        : _localization.Get("Coaching.MapControlEditor.Save.Source.Validation");

    public string PackName => _mainInformation?.PackName.Trim() ?? "—";

    public string ScopeSummary => _scope is null
        ? "—"
        : $"{_scope.RolesSummary} · {_scope.TeamSideSummary}";

    public int RuleCount => _content?.RuleCount ?? 0;

    public int TimedReminderCount => _content?.TimedReminders.Count ?? 0;

    public int PersistentTipCount => _content?.PersistentTips.Count ?? 0;

    public string PackPatch => _mainInformation?.GamePatch ?? "—";

    public string PackLanguage => _localization.Format(
        "Coaching.MatchupEditor.Save.Card.Language",
        _mainInformation?.ContentLanguageCode.StartsWith(
            "ru",
            StringComparison.OrdinalIgnoreCase) == true
            ? "Русский"
            : "English");

    public void BeginNew()
    {
        _targetPackId = null;
        SelectedSourceKind = CoachPackSourceKind.PersonalExperience;
        SourceTitle = string.Empty;
        SourceAuthor = string.Empty;
        SourceUrl = string.Empty;
        ResetSaveResult();
    }

    public void Update(
        MapControlMainInformationViewModel mainInformation,
        MapControlScopeViewModel scope,
        MapControlContentViewModel content)
    {
        _mainInformation = mainInformation ?? throw new ArgumentNullException(nameof(mainInformation));
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        _content = content ?? throw new ArgumentNullException(nameof(content));
        NotifySummaryChanged();
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
        SaveStatus = _localization.Get("Coaching.MapControlEditor.Save.Status.Saving");
        try
        {
            var result = await _saveMapControlPack.ExecuteAsync(
                CreatePack(),
                state,
                cancellationToken);
            _targetPackId = result.PackId;
            SaveStatus = _localization.Get(state == LocalCoachPackState.Draft
                ? "Coaching.MapControlEditor.Save.Status.DraftSaved"
                : "Coaching.MapControlEditor.Save.Status.EnabledSaved");
            return true;
        }
        catch (Exception exception) when (exception is IOException
                                          or UnauthorizedAccessException
                                          or InvalidDataException)
        {
            HasSaveError = true;
            SaveStatus = _localization.Get("Coaching.MapControlEditor.Save.Status.Failed");
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public void UpdateLanguage()
    {
        NotifySourceChanged();
        NotifySummaryChanged();
    }

    private MapControlPack CreatePack()
    {
        var mainInformation = _mainInformation
            ?? throw new InvalidOperationException("Основная информация не подготовлена.");
        var scope = _scope
            ?? throw new InvalidOperationException("Область применения не подготовлена.");
        var content = _content
            ?? throw new InvalidOperationException("Содержание контроля карты не подготовлено.");
        return _packFactory.Create(
            mainInformation,
            scope,
            content,
            new MapControlSourceDraft(
                SelectedSourceKind,
                SourceTitle,
                SourceUrl,
                SourceAuthor),
            _targetPackId);
    }

    private void ResetSaveResult()
    {
        SaveStatus = string.Empty;
        HasSaveError = false;
    }

    private void NotifySourceChanged()
    {
        OnPropertyChanged(nameof(IsPersonalExperience));
        OnPropertyChanged(nameof(IsArticle));
        OnPropertyChanged(nameof(IsVideo));
        OnPropertyChanged(nameof(IsTrainingMaterial));
        OnPropertyChanged(nameof(HasExternalSource));
        OnPropertyChanged(nameof(IsSourceComplete));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SourceValidationMessage));
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(PackName));
        OnPropertyChanged(nameof(ScopeSummary));
        OnPropertyChanged(nameof(RuleCount));
        OnPropertyChanged(nameof(TimedReminderCount));
        OnPropertyChanged(nameof(PersistentTipCount));
        OnPropertyChanged(nameof(PackPatch));
        OnPropertyChanged(nameof(PackLanguage));
    }

    private static bool IsOptionalHttpUrl(string value) =>
        string.IsNullOrWhiteSpace(value)
        || Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https";
}
