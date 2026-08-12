using TempoCue.App.Localization;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Симулирует совет черновика для одного уровня без изменения самого матчапа.</summary>
public sealed class MatchupVerificationViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly MatchupPhaseSelector _phaseSelector;
    private IReadOnlyList<MatchupPhaseViewModel> _phases = [];
    private IReadOnlyList<MatchupPersistentTipViewModel> _persistentTips = [];
    private IReadOnlyList<MatchupVerificationPresetViewModel> _presets = [];
    private IReadOnlyList<MatchupPersistentTipViewModel> _visiblePersistentTips = [];
    private MatchupPhaseValidationViewModel? _validation;
    private MatchupPhaseViewModel? _activePhase;
    private MatchupPhaseSelectionState _selectionState = MatchupPhaseSelectionState.NotFound;
    private int _selectedLevel = MatchupPhaseSetValidator.MinimumGameLevel;
    private string _playerChampionName = string.Empty;
    private string _opponentChampionName = string.Empty;
    private string _author = string.Empty;

    public MatchupVerificationViewModel(
        LocalizationService localization,
        MatchupPhaseSelector phaseSelector)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        _phaseSelector = phaseSelector ?? throw new ArgumentNullException(nameof(phaseSelector));
    }

    public MatchupPhaseValidationViewModel? Validation
    {
        get => _validation;
        private set => SetProperty(ref _validation, value);
    }

    public IReadOnlyList<MatchupVerificationPresetViewModel> Presets
    {
        get => _presets;
        private set => SetProperty(ref _presets, value);
    }

    public IReadOnlyList<MatchupPersistentTipViewModel> VisiblePersistentTips
    {
        get => _visiblePersistentTips;
        private set
        {
            if (SetProperty(ref _visiblePersistentTips, value))
            {
                OnPropertyChanged(nameof(HasVisiblePersistentTips));
            }
        }
    }

    public MatchupPhaseViewModel? ActivePhase
    {
        get => _activePhase;
        private set
        {
            if (SetProperty(ref _activePhase, value))
            {
                NotifyActivePhaseChanged();
            }
        }
    }

    public MatchupPhaseSelectionState SelectionState
    {
        get => _selectionState;
        private set
        {
            if (SetProperty(ref _selectionState, value))
            {
                OnPropertyChanged(nameof(SelectionTitle));
                OnPropertyChanged(nameof(SelectionDescription));
            }
        }
    }

    public int SelectedLevel
    {
        get => _selectedLevel;
        set
        {
            var normalizedLevel = Math.Clamp(
                value,
                MatchupPhaseSetValidator.MinimumGameLevel,
                MatchupPhaseSetValidator.MaximumGameLevel);
            if (SetProperty(ref _selectedLevel, normalizedLevel))
            {
                RefreshSelection();
            }
        }
    }

    public bool CanDecreaseLevel => SelectedLevel > MatchupPhaseSetValidator.MinimumGameLevel;

    public bool CanIncreaseLevel => SelectedLevel < MatchupPhaseSetValidator.MaximumGameLevel;

    public bool HasActivePhase => ActivePhase is not null;

    public bool HasWhyNow => ActivePhase?.HasWhyNow == true;

    public bool HasNextGoal => ActivePhase?.HasNextGoal == true;

    public bool HasRecommendedItems => ActivePhase?.HasRecommendedItems == true;

    public bool HasVisiblePersistentTips => VisiblePersistentTips.Count > 0;

    public bool CanContinue => _phases.Count > 0 && Validation?.HasErrors == false;

    public string ActivePhaseTitle => ActivePhase is null
        ? SelectionTitle
        : _localization.Format(
            "Coaching.MatchupEditor.Verification.ActivePhase",
            ActivePhase.Order,
            ActivePhase.Name);

    public string ActivePhaseDescription => ActivePhase is null
        ? SelectionDescription
        : _localization.Format(
            "Coaching.MatchupEditor.Verification.ActivePhaseDescription",
            ActivePhase.LevelRange);

    public string SelectionTitle => _localization.Get(SelectionState switch
    {
        MatchupPhaseSelectionState.Selected => "Coaching.MatchupEditor.Verification.Selection.Selected",
        MatchupPhaseSelectionState.NotFound => "Coaching.MatchupEditor.Verification.Selection.NotFound",
        MatchupPhaseSelectionState.Ambiguous => "Coaching.MatchupEditor.Verification.Selection.Ambiguous",
        _ => throw new ArgumentOutOfRangeException()
    });

    public string SelectionDescription => _localization.Format(
        SelectionState switch
        {
            MatchupPhaseSelectionState.Selected => "Coaching.MatchupEditor.Verification.Selection.SelectedDescription",
            MatchupPhaseSelectionState.NotFound => "Coaching.MatchupEditor.Verification.Selection.NotFoundDescription",
            MatchupPhaseSelectionState.Ambiguous => "Coaching.MatchupEditor.Verification.Selection.AmbiguousDescription",
            _ => throw new ArgumentOutOfRangeException()
        },
        SelectedLevel);

    public string AdviceSource => _localization.Format(
        "Coaching.MatchupEditor.Verification.AdviceSource",
        _playerChampionName,
        _opponentChampionName,
        _author);

    public string PhaseOrderLabel => ActivePhase is null
        ? string.Empty
        : _localization.Format(
            "Coaching.MatchupEditor.Verification.Advice.Phase",
            ActivePhase.Order);

    public void Update(
        MatchupMainInformationViewModel mainInformation,
        MatchupChampionsAndRoleViewModel championsAndRole,
        MatchupContentViewModel content)
    {
        ArgumentNullException.ThrowIfNull(mainInformation);
        ArgumentNullException.ThrowIfNull(championsAndRole);
        ArgumentNullException.ThrowIfNull(content);

        _phases = content.Phases.ToArray();
        _persistentTips = content.PersistentTips.Tips.ToArray();
        _playerChampionName = championsAndRole.SelectedPlayerChampion?.DisplayName ?? string.Empty;
        _opponentChampionName = championsAndRole.SelectedOpponentChampion?.DisplayName ?? string.Empty;
        _author = mainInformation.Author.Trim();
        Validation = content.Validation;
        Presets = _phases
            .Select(static phase => phase.MinimumLevel)
            .Distinct()
            .Order()
            .Select(CreatePreset)
            .ToArray();
        RefreshSelection();
        OnPropertyChanged(nameof(CanContinue));
        OnPropertyChanged(nameof(AdviceSource));
        OnPropertyChanged(nameof(PhaseOrderLabel));
    }

    public void SelectPreset(MatchupVerificationPresetViewModel preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        SelectedLevel = preset.Level;
    }

    public void DecreaseLevel() => SelectedLevel--;

    public void IncreaseLevel() => SelectedLevel++;

    public void UpdateLanguage()
    {
        Presets = Presets.Select(preset => CreatePreset(preset.Level)).ToArray();
        OnPropertyChanged(nameof(ActivePhaseTitle));
        OnPropertyChanged(nameof(ActivePhaseDescription));
        OnPropertyChanged(nameof(SelectionTitle));
        OnPropertyChanged(nameof(SelectionDescription));
        OnPropertyChanged(nameof(AdviceSource));
        OnPropertyChanged(nameof(PhaseOrderLabel));
    }

    private MatchupVerificationPresetViewModel CreatePreset(int level) => new(
        level,
        _localization.Format("Coaching.MatchupEditor.Verification.Preset", level),
        level == SelectedLevel);

    private void RefreshSelection()
    {
        var result = _phaseSelector.Select(
            _phases.Select(static phase => new MatchupPhaseCondition(
                phase.Id,
                phase.MinimumLevel,
                phase.MaximumLevel)).ToArray(),
            SelectedLevel);
        SelectionState = result.State;
        ActivePhase = result.SelectedPhaseId is string selectedPhaseId
            ? _phases.FirstOrDefault(phase => phase.Id == selectedPhaseId)
            : null;
        Presets = Presets
            .Select(preset => preset with { IsSelected = preset.Level == SelectedLevel })
            .ToArray();
        VisiblePersistentTips = _persistentTips
            .Where(tip => tip.ApplicablePhaseId is null || tip.ApplicablePhaseId == ActivePhase?.Id)
            .ToArray();
        OnPropertyChanged(nameof(CanDecreaseLevel));
        OnPropertyChanged(nameof(CanIncreaseLevel));
        OnPropertyChanged(nameof(SelectionTitle));
        OnPropertyChanged(nameof(SelectionDescription));
    }

    private void NotifyActivePhaseChanged()
    {
        OnPropertyChanged(nameof(HasActivePhase));
        OnPropertyChanged(nameof(HasWhyNow));
        OnPropertyChanged(nameof(HasNextGoal));
        OnPropertyChanged(nameof(HasRecommendedItems));
        OnPropertyChanged(nameof(ActivePhaseTitle));
        OnPropertyChanged(nameof(ActivePhaseDescription));
        OnPropertyChanged(nameof(PhaseOrderLabel));
    }
}

public sealed record MatchupVerificationPresetViewModel(
    int Level,
    string Label,
    bool IsSelected);
