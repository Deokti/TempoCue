using TempoCue.App.Localization;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Изолированная рабочая копия формы создания или редактирования фазы.</summary>
public sealed class MatchupPhaseEditorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private bool _isOpen;
    private string? _editingPhaseId;
    private string _name = string.Empty;
    private PowerState _selectedPowerState = PowerState.Dangerous;
    private int _minimumLevel = 1;
    private int _maximumLevel = 18;
    private bool _extendsToEnd = true;
    private string _primaryAdvice = string.Empty;
    private string _whyNow = string.Empty;
    private string _nextGoal = string.Empty;
    private IReadOnlyList<MatchupItemDraft> _startItems = [];
    private IReadOnlyList<MatchupItemDraft> _endItems = [];
    private IReadOnlyList<MatchupPowerStateOptionViewModel> _powerStateOptions = [];

    public MatchupPhaseEditorViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        RebuildPowerStateOptions();
    }

    public bool IsOpen
    {
        get => _isOpen;
        private set => SetProperty(ref _isOpen, value);
    }

    public string? EditingPhaseId
    {
        get => _editingPhaseId;
        private set
        {
            if (SetProperty(ref _editingPhaseId, value))
            {
                OnPropertyChanged(nameof(IsEditingExisting));
                OnPropertyChanged(nameof(Header));
            }
        }
    }

    public bool IsEditingExisting => EditingPhaseId is not null;

    public string Header => IsEditingExisting
        ? _localization.Format("Coaching.MatchupEditor.Content.Editor.EditTitle", Name)
        : _localization.Get("Coaching.MatchupEditor.Content.Editor.AddTitle");

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(Header));
                NotifyValidationChanged(nameof(NameLengthFeedback));
            }
        }
    }

    public IReadOnlyList<MatchupPowerStateOptionViewModel> PowerStateOptions
    {
        get => _powerStateOptions;
        private set => SetProperty(ref _powerStateOptions, value);
    }

    public PowerState SelectedPowerState
    {
        get => _selectedPowerState;
        set => SetProperty(ref _selectedPowerState, value);
    }

    public int MinimumLevel
    {
        get => _minimumLevel;
        private set
        {
            if (SetProperty(ref _minimumLevel, value))
            {
                if (!_extendsToEnd && _maximumLevel < value)
                {
                    MaximumLevel = value;
                }

                NotifyValidationChanged();
                OnPropertyChanged(nameof(MeaningSummary));
            }
        }
    }

    public int MaximumLevel
    {
        get => _maximumLevel;
        private set
        {
            if (SetProperty(ref _maximumLevel, value))
            {
                NotifyValidationChanged();
                OnPropertyChanged(nameof(MeaningSummary));
            }
        }
    }

    public bool ExtendsToEnd
    {
        get => _extendsToEnd;
        set
        {
            if (SetProperty(ref _extendsToEnd, value))
            {
                if (!value && MaximumLevel < MinimumLevel)
                {
                    MaximumLevel = MinimumLevel;
                }

                NotifyValidationChanged();
                OnPropertyChanged(nameof(MeaningSummary));
            }
        }
    }

    public string PrimaryAdvice
    {
        get => _primaryAdvice;
        set
        {
            if (SetProperty(ref _primaryAdvice, value ?? string.Empty))
            {
                NotifyValidationChanged(nameof(PrimaryAdviceLengthFeedback));
            }
        }
    }

    public string WhyNow
    {
        get => _whyNow;
        set
        {
            if (SetProperty(ref _whyNow, value ?? string.Empty))
            {
                NotifyValidationChanged();
            }
        }
    }

    public string NextGoal
    {
        get => _nextGoal;
        set
        {
            if (SetProperty(ref _nextGoal, value ?? string.Empty))
            {
                NotifyValidationChanged();
            }
        }
    }

    public IReadOnlyList<MatchupItemDraft> StartItems
    {
        get => _startItems;
        private set
        {
            if (SetProperty(ref _startItems, value))
            {
                OnPropertyChanged(nameof(HasStartItems));
                OnPropertyChanged(nameof(MeaningSummary));
                OnPropertyChanged(nameof(HasConflictingItemConditions));
                NotifyValidationChanged();
            }
        }
    }

    public bool HasStartItems => StartItems.Count > 0;

    public IReadOnlyList<MatchupItemDraft> EndItems
    {
        get => _endItems;
        private set
        {
            if (SetProperty(ref _endItems, value))
            {
                OnPropertyChanged(nameof(HasEndItems));
                OnPropertyChanged(nameof(MeaningSummary));
                OnPropertyChanged(nameof(HasConflictingItemConditions));
                NotifyValidationChanged();
            }
        }
    }

    public bool HasEndItems => EndItems.Count > 0;

    public bool HasConflictingItemConditions =>
        StartItems.Select(static item => item.Id)
            .Intersect(EndItems.Select(static item => item.Id))
            .Any();

    public TextLengthFeedback NameLengthFeedback => TextLengthFeedback.Create(
        Name,
        3,
        80,
        _localization);

    public TextLengthFeedback PrimaryAdviceLengthFeedback => TextLengthFeedback.Create(
        PrimaryAdvice,
        3,
        200,
        _localization);

    public bool CanSave =>
        TextLengthFeedback.IsWithin(Name, 3, 80)
        && TextLengthFeedback.IsWithin(PrimaryAdvice, 3, 200)
        && WhyNow.Trim().Length <= 240
        && NextGoal.Trim().Length <= 160
        && !HasConflictingItemConditions
        && MinimumLevel is >= 1 and <= 18
        && (ExtendsToEnd || MaximumLevel is >= 1 and <= 18 && MaximumLevel >= MinimumLevel);

    public string MeaningSummary
    {
        get
        {
            var summary = ExtendsToEnd
                ? _localization.Format(
                    "Coaching.MatchupEditor.Content.Editor.MeaningToEnd",
                    MinimumLevel)
                : _localization.Format(
                    "Coaching.MatchupEditor.Content.Editor.MeaningRange",
                    MinimumLevel,
                    MaximumLevel);
            if (HasStartItems)
            {
                summary += _localization.Format(
                    "Coaching.MatchupEditor.Content.Editor.MeaningStartItemSuffix",
                    JoinItemNames(StartItems));
            }

            if (HasEndItems)
            {
                summary += _localization.Format(
                    "Coaching.MatchupEditor.Content.Editor.MeaningEndItemSuffix",
                    JoinItemNames(EndItems));
            }

            return summary;
        }
    }

    public void BeginNew()
    {
        EditingPhaseId = null;
        Name = string.Empty;
        SelectedPowerState = PowerState.Dangerous;
        MinimumLevel = 1;
        MaximumLevel = 18;
        ExtendsToEnd = true;
        PrimaryAdvice = string.Empty;
        WhyNow = string.Empty;
        NextGoal = string.Empty;
        StartItems = [];
        EndItems = [];
        IsOpen = true;
    }

    public void BeginEdit(MatchupPhaseDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EditingPhaseId = draft.Id;
        Name = draft.Name;
        SelectedPowerState = draft.PowerState;
        MinimumLevel = draft.MinimumLevel;
        MaximumLevel = draft.MaximumLevel ?? 18;
        ExtendsToEnd = draft.MaximumLevel is null;
        PrimaryAdvice = draft.PrimaryAdvice;
        WhyNow = draft.WhyNow;
        NextGoal = draft.NextGoal;
        StartItems = draft.StartItems.ToArray();
        EndItems = draft.EndItems.ToArray();
        IsOpen = true;
    }

    public MatchupPhaseDraft CreateSnapshot(string id)
    {
        if (!CanSave)
        {
            throw new InvalidOperationException("Нельзя сохранить незаполненную фазу.");
        }

        return new MatchupPhaseDraft(
            id,
            Name.Trim(),
            SelectedPowerState,
            MinimumLevel,
            ExtendsToEnd ? null : MaximumLevel,
            PrimaryAdvice.Trim(),
            WhyNow.Trim(),
            NextGoal.Trim(),
            StartItems.ToArray(),
            EndItems.ToArray());
    }

    public void SetStartItems(IEnumerable<ItemOptionViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        StartItems = CreateItemDrafts(items);
    }

    public void RemoveStartItem(MatchupItemDraft item)
    {
        ArgumentNullException.ThrowIfNull(item);
        StartItems = StartItems.Where(existing => existing.Id != item.Id).ToArray();
    }

    public void SetEndItems(IEnumerable<ItemOptionViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        EndItems = CreateItemDrafts(items);
    }

    public void RemoveEndItem(MatchupItemDraft item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EndItems = EndItems.Where(existing => existing.Id != item.Id).ToArray();
    }

    public void Close() => IsOpen = false;

    public void DecreaseMinimumLevel() =>
        MinimumLevel = Math.Max(1, MinimumLevel - 1);

    public void IncreaseMinimumLevel() =>
        MinimumLevel = Math.Min(18, MinimumLevel + 1);

    public void DecreaseMaximumLevel() =>
        MaximumLevel = Math.Max(MinimumLevel, MaximumLevel - 1);

    public void IncreaseMaximumLevel() =>
        MaximumLevel = Math.Min(18, MaximumLevel + 1);

    public void UpdateLanguage()
    {
        RebuildPowerStateOptions();
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(MeaningSummary));
        OnPropertyChanged(nameof(NameLengthFeedback));
        OnPropertyChanged(nameof(PrimaryAdviceLengthFeedback));
    }

    private void RebuildPowerStateOptions() =>
        PowerStateOptions =
        [
            new(PowerState.Dangerous, _localization.Get("Coaching.Power.Dangerous")),
            new(PowerState.Cautious, _localization.Get("Coaching.Power.Cautious")),
            new(PowerState.Playable, _localization.Get("Coaching.Power.Playable"))
        ];

    private void NotifyValidationChanged(string? feedbackPropertyName = null)
    {
        OnPropertyChanged(nameof(CanSave));
        if (feedbackPropertyName is not null)
        {
            OnPropertyChanged(feedbackPropertyName);
        }
    }

    private static IReadOnlyList<MatchupItemDraft> CreateItemDrafts(
        IEnumerable<ItemOptionViewModel> items) => items
        .DistinctBy(static item => item.Id)
        .Select(static item => new MatchupItemDraft(item.Id, item.DisplayName, item.ImageUri))
        .ToArray();

    private static string JoinItemNames(IEnumerable<MatchupItemDraft> items) =>
        string.Join(", ", items.Select(static item => item.DisplayName));
}

public sealed record MatchupPowerStateOptionViewModel(
    PowerState Value,
    string DisplayName);
