using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Изолированная рабочая копия новой или выбранной постоянной подсказки.</summary>
public sealed class MatchupPersistentTipEditorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private bool _isOpen;
    private string? _editingTipId;
    private string _title = string.Empty;
    private string _text = string.Empty;
    private string? _selectedPhaseId;
    private bool _usesCustomSource;
    private string _customSource = string.Empty;
    private IReadOnlyList<MatchupPersistentTipScopeOptionViewModel> _scopeOptions = [];

    public MatchupPersistentTipEditorViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        UpdatePhaseOptions([]);
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string? EditingTipId
    {
        get => _editingTipId;
        private set
        {
            if (SetProperty(ref _editingTipId, value))
            {
                OnPropertyChanged(nameof(IsEditingExisting));
                OnPropertyChanged(nameof(Header));
            }
        }
    }

    public bool IsEditingExisting => EditingTipId is not null;

    public string Header => IsEditingExisting
        ? _localization.Format("Coaching.MatchupEditor.Persistent.Editor.EditTitle", Title)
        : _localization.Get("Coaching.MatchupEditor.Persistent.Editor.AddTitle");

    public string Title
    {
        get => _title;
        set
        {
            if (SetProperty(ref _title, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Header));
                NotifyValidationChanged(nameof(TitleLengthFeedback));
            }
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            if (SetProperty(ref _text, value ?? string.Empty))
            {
                NotifyValidationChanged(nameof(TextLengthFeedback));
            }
        }
    }

    public IReadOnlyList<MatchupPersistentTipScopeOptionViewModel> ScopeOptions
    {
        get => _scopeOptions;
        private set => SetProperty(ref _scopeOptions, value);
    }

    public string? SelectedPhaseId
    {
        get => _selectedPhaseId;
        set => SetProperty(ref _selectedPhaseId, value);
    }

    public bool UsesCustomSource
    {
        get => _usesCustomSource;
        set
        {
            if (SetProperty(ref _usesCustomSource, value))
            {
                NotifyValidationChanged();
            }
        }
    }

    public string CustomSource
    {
        get => _customSource;
        set
        {
            if (SetProperty(ref _customSource, value ?? string.Empty))
            {
                NotifyValidationChanged(nameof(CustomSourceLengthFeedback));
            }
        }
    }

    public TextLengthFeedback TitleLengthFeedback => TextLengthFeedback.Create(
        Title,
        3,
        80,
        _localization);

    public TextLengthFeedback TextLengthFeedback => TextLengthFeedback.Create(
        Text,
        3,
        240,
        _localization);

    public TextLengthFeedback CustomSourceLengthFeedback => TextLengthFeedback.Create(
        CustomSource,
        3,
        240,
        _localization);

    public bool CanSave =>
        TextLengthFeedback.IsWithin(Title, 3, 80)
        && TextLengthFeedback.IsWithin(Text, 3, 240)
        && (!UsesCustomSource || TextLengthFeedback.IsWithin(CustomSource, 3, 240));

    public void BeginNew()
    {
        EditingTipId = null;
        Title = _localization.Get("Coaching.MatchupEditor.Persistent.DefaultTitle");
        Text = string.Empty;
        SelectedPhaseId = null;
        UsesCustomSource = false;
        CustomSource = string.Empty;
        IsOpen = true;
    }

    public void BeginEdit(MatchupPersistentTipDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EditingTipId = draft.Id;
        Title = draft.Title;
        Text = draft.Text;
        SelectedPhaseId = ScopeOptions.Any(option => option.PhaseId == draft.ApplicablePhaseId)
            ? draft.ApplicablePhaseId
            : null;
        UsesCustomSource = draft.CustomSource is not null;
        CustomSource = draft.CustomSource ?? string.Empty;
        IsOpen = true;
    }

    public MatchupPersistentTipDraft CreateSnapshot(string id)
    {
        if (!CanSave)
        {
            throw new InvalidOperationException("Нельзя сохранить незаполненную постоянную подсказку.");
        }

        return new MatchupPersistentTipDraft(
            id,
            Title.Trim(),
            Text.Trim(),
            SelectedPhaseId,
            UsesCustomSource ? CustomSource.Trim() : null);
    }

    public void UpdatePhaseOptions(IEnumerable<MatchupPhaseViewModel> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        ScopeOptions =
        [
            new(null, _localization.Get("Coaching.MatchupEditor.Persistent.Scope.WholeMatch")),
            .. phases.Select(phase => new MatchupPersistentTipScopeOptionViewModel(
                phase.Id,
                _localization.Format(
                    "Coaching.MatchupEditor.Persistent.Scope.Phase",
                    phase.Order,
                    phase.Name)))
        ];

        if (!ScopeOptions.Any(option => option.PhaseId == SelectedPhaseId))
        {
            SelectedPhaseId = null;
        }
    }

    public void Close() => IsOpen = false;

    public void UpdateLanguage(IEnumerable<MatchupPhaseViewModel> phases)
    {
        UpdatePhaseOptions(phases);
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(TitleLengthFeedback));
        OnPropertyChanged(nameof(TextLengthFeedback));
        OnPropertyChanged(nameof(CustomSourceLengthFeedback));
    }

    private void NotifyValidationChanged(string? feedbackPropertyName = null)
    {
        OnPropertyChanged(nameof(CanSave));
        if (feedbackPropertyName is not null)
        {
            OnPropertyChanged(feedbackPropertyName);
        }
    }
}
