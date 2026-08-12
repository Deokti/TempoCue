using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Хранит изолированную рабочую копию одного правила объекта.</summary>
public sealed class MapControlObjectiveRuleEditorViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private string? _editingRuleId;
    private MapObjectiveTiming? _timing;
    private MapObjectiveReminderTrigger _selectedTrigger;
    private MapObjectiveReminderRecurrence _selectedRecurrence;
    private MapObjectiveReminderPriority _selectedPriority;
    private int _leadTimeSeconds = 15;
    private string _message = string.Empty;
    private string _whyNow = string.Empty;
    private string _nextGoal = string.Empty;
    private bool _isEnabled = true;
    private IReadOnlyList<MapControlRuleTriggerOptionViewModel> _triggerOptions = [];
    private IReadOnlyList<MapControlRuleRecurrenceOptionViewModel> _recurrenceOptions = [];
    private IReadOnlyList<MapControlRulePriorityOptionViewModel> _priorityOptions = [];

    public MapControlObjectiveRuleEditorViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        RebuildOptions();
    }

    public bool IsOpen => _timing is not null;

    public bool IsEditingExisting => EditingRuleId is not null;

    public string? EditingRuleId
    {
        get => _editingRuleId;
        private set
        {
            if (SetProperty(ref _editingRuleId, value))
            {
                OnPropertyChanged(nameof(IsEditingExisting));
            }
        }
    }

    public string Header => _localization.Format(
        "Coaching.MapControlEditor.Content.Editor.Title",
        ObjectiveName);

    public string ObjectiveName => _timing is null
        ? string.Empty
        : MapControlContentText.ObjectiveName(_timing.Objective, _localization);

    public string TimingNote => _timing is null
        ? string.Empty
        : _localization.Format(
            _timing.Respawns
                ? "Coaching.MapControlEditor.Content.Editor.Timing.Respawns"
                : "Coaching.MapControlEditor.Content.Editor.Timing.Once",
            MapControlContentText.FormatTime(_timing.InitialSpawnTime),
            _timing.RespawnDelay is { } delay ? MapControlContentText.FormatTime(delay) : string.Empty);

    public IReadOnlyList<MapControlRuleTriggerOptionViewModel> TriggerOptions
    {
        get => _triggerOptions;
        private set => SetProperty(ref _triggerOptions, value);
    }

    public IReadOnlyList<MapControlRuleRecurrenceOptionViewModel> RecurrenceOptions
    {
        get => _recurrenceOptions;
        private set => SetProperty(ref _recurrenceOptions, value);
    }

    public IReadOnlyList<MapControlRulePriorityOptionViewModel> PriorityOptions
    {
        get => _priorityOptions;
        private set => SetProperty(ref _priorityOptions, value);
    }

    public MapObjectiveReminderTrigger SelectedTrigger
    {
        get => _selectedTrigger;
        set
        {
            if (!IsTriggerAvailable(value) || !SetProperty(ref _selectedTrigger, value))
            {
                return;
            }

            if (!UsesLeadTime)
            {
                LeadTimeSeconds = 0;
            }
            else if (LeadTimeSeconds == 0)
            {
                LeadTimeSeconds = 15;
            }

            if (!CanRepeat)
            {
                SelectedRecurrence = MapObjectiveReminderRecurrence.OncePerMatch;
            }

            OnPropertyChanged(nameof(UsesLeadTime));
            OnPropertyChanged(nameof(IsInitialTrigger));
            OnPropertyChanged(nameof(CanRepeat));
            RebuildRecurrenceOptions();
            NotifyStateChanged();
        }
    }

    public MapObjectiveReminderRecurrence SelectedRecurrence
    {
        get => _selectedRecurrence;
        set
        {
            var normalized = !CanRepeat
                ? MapObjectiveReminderRecurrence.OncePerMatch
                : value;
            if (SetProperty(ref _selectedRecurrence, normalized))
            {
                NotifyStateChanged();
            }
        }
    }

    public MapObjectiveReminderPriority SelectedPriority
    {
        get => _selectedPriority;
        set
        {
            if (SetProperty(ref _selectedPriority, value))
            {
                NotifyStateChanged();
            }
        }
    }

    public int LeadTimeSeconds
    {
        get => _leadTimeSeconds;
        private set
        {
            var normalized = UsesLeadTime ? Math.Clamp(value, 15, MaximumLeadTimeSeconds) : 0;
            if (SetProperty(ref _leadTimeSeconds, normalized))
            {
                NotifyStateChanged();
            }
        }
    }

    public bool UsesLeadTime => SelectedTrigger is
        MapObjectiveReminderTrigger.BeforeInitialSpawn or
        MapObjectiveReminderTrigger.BeforeRespawn;

    public bool IsInitialTrigger => SelectedTrigger is
        MapObjectiveReminderTrigger.BeforeInitialSpawn or
        MapObjectiveReminderTrigger.AtInitialSpawn;

    public bool CanRepeat =>
        !IsInitialTrigger || _timing?.Respawns == true;

    public string Message
    {
        get => _message;
        set
        {
            if (SetProperty(ref _message, value ?? string.Empty))
            {
                NotifyStateChanged();
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
                NotifyStateChanged();
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
                NotifyStateChanged();
            }
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public bool CanSave =>
        _timing is not null
        && Message.Trim().Length is >= 3 and <= 200
        && WhyNow.Trim().Length is >= 3 and <= 240
        && NextGoal.Trim().Length is >= 3 and <= 160
        && IsTriggerAvailable(SelectedTrigger);

    public string LiveSummary => _timing is null
        ? string.Empty
        : _localization.Format(
            "Coaching.MapControlEditor.Content.Editor.LiveSummary",
            MapControlContentText.TriggerSummary(CreateRule("preview"), _localization),
            MapControlContentText.RecurrenceName(SelectedRecurrence, _localization));

    private int MaximumLeadTimeSeconds => (int)Math.Max(
        15,
        (SelectedTrigger == MapObjectiveReminderTrigger.BeforeRespawn
            ? _timing?.RespawnDelay
            : _timing?.InitialSpawnTime)?.TotalSeconds ?? 15);

    public void BeginNew(MapObjectiveTiming timing)
    {
        _timing = timing ?? throw new ArgumentNullException(nameof(timing));
        EditingRuleId = null;
        _selectedTrigger = MapObjectiveReminderTrigger.BeforeInitialSpawn;
        _selectedRecurrence = MapObjectiveReminderRecurrence.OncePerMatch;
        _selectedPriority = MapObjectiveReminderPriority.Caution;
        _leadTimeSeconds = 15;
        Message = string.Empty;
        WhyNow = string.Empty;
        NextGoal = string.Empty;
        IsEnabled = true;
        RebuildOptions();
        NotifyEditorChanged();
    }

    public void BeginEdit(MapObjectiveReminderRule rule, MapObjectiveTiming timing)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _timing = timing ?? throw new ArgumentNullException(nameof(timing));
        EditingRuleId = rule.Id;
        _selectedTrigger = rule.Trigger;
        _selectedRecurrence = rule.Recurrence;
        _selectedPriority = rule.Priority;
        _leadTimeSeconds = (int)rule.LeadTime.TotalSeconds;
        Message = rule.Message;
        WhyNow = rule.WhyNow;
        NextGoal = rule.NextGoal;
        IsEnabled = rule.IsEnabled;
        RebuildOptions();
        NotifyEditorChanged();
    }

    public void Close()
    {
        _timing = null;
        EditingRuleId = null;
        NotifyEditorChanged();
    }

    public void IncreaseLeadTime() => LeadTimeSeconds += 15;

    public void DecreaseLeadTime() => LeadTimeSeconds -= 15;

    public MapObjectiveReminderRule CreateRule(string id)
    {
        if (_timing is null)
        {
            throw new InvalidOperationException("Objective rule editor is closed.");
        }

        return new MapObjectiveReminderRule(
            id,
            _timing.Objective,
            SelectedTrigger,
            TimeSpan.FromSeconds(LeadTimeSeconds),
            SelectedRecurrence,
            SelectedPriority,
            Message.Trim(),
            WhyNow.Trim(),
            NextGoal.Trim(),
            IsEnabled);
    }

    public void UpdateLanguage()
    {
        RebuildOptions();
        NotifyEditorChanged();
    }

    private bool IsTriggerAvailable(MapObjectiveReminderTrigger trigger) =>
        trigger != MapObjectiveReminderTrigger.BeforeRespawn || _timing?.Respawns == true;

    private void RebuildOptions()
    {
        TriggerOptions = Enum.GetValues<MapObjectiveReminderTrigger>()
            .Select(value => new MapControlRuleTriggerOptionViewModel(
                value,
                MapControlContentText.TriggerName(value, _localization),
                MapControlContentText.TriggerDescription(value, _localization),
                IsTriggerAvailable(value)))
            .ToArray();
        RebuildRecurrenceOptions();
        PriorityOptions = Enum.GetValues<MapObjectiveReminderPriority>()
            .Select(value => new MapControlRulePriorityOptionViewModel(
                value,
                MapControlContentText.PriorityName(value, _localization)))
            .ToArray();
    }

    private void RebuildRecurrenceOptions()
    {
        RecurrenceOptions = Enum.GetValues<MapObjectiveReminderRecurrence>()
            .Select(value => new MapControlRuleRecurrenceOptionViewModel(
                value,
                MapControlContentText.RecurrenceName(value, _localization),
                MapControlContentText.RecurrenceDescription(
                    value,
                    SelectedTrigger,
                    CanRepeat,
                    _localization),
                value == MapObjectiveReminderRecurrence.OncePerMatch || CanRepeat))
            .ToArray();
    }

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(LiveSummary));
    }

    private void NotifyEditorChanged()
    {
        OnPropertyChanged(nameof(IsOpen));
        OnPropertyChanged(nameof(Header));
        OnPropertyChanged(nameof(ObjectiveName));
        OnPropertyChanged(nameof(TimingNote));
        OnPropertyChanged(nameof(UsesLeadTime));
        OnPropertyChanged(nameof(IsInitialTrigger));
        OnPropertyChanged(nameof(CanRepeat));
        NotifyStateChanged();
    }
}
