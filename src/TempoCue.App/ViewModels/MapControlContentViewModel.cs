using System.Collections.ObjectModel;
using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Владеет правилами объектов третьего шага и операциями над их порядком.</summary>
public sealed class MapControlContentViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ObservableCollection<MapControlObjectiveRuleViewModel> _rules = [];
    private IReadOnlyList<MapControlObjectiveOptionViewModel> _objectiveOptions = [];
    private MapObjectiveTimingSet? _timingSet;
    private bool _isObjectivePickerOpen;
    private int _nextRuleNumber = 1;

    public MapControlContentViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Rules = new ReadOnlyObservableCollection<MapControlObjectiveRuleViewModel>(_rules);
        Editor = new MapControlObjectiveRuleEditorViewModel(localization);
        Editor.PropertyChanged += Editor_PropertyChanged;
    }

    public ReadOnlyObservableCollection<MapControlObjectiveRuleViewModel> Rules { get; }

    public MapControlObjectiveRuleEditorViewModel Editor { get; }

    public IReadOnlyList<MapControlObjectiveOptionViewModel> ObjectiveOptions
    {
        get => _objectiveOptions;
        private set => SetProperty(ref _objectiveOptions, value);
    }

    public bool IsObjectivePickerOpen
    {
        get => _isObjectivePickerOpen;
        private set => SetProperty(ref _isObjectivePickerOpen, value);
    }

    public int RuleCount => _rules.Count;

    public bool HasRules => RuleCount > 0;

    public bool IsComplete => HasRules && !Editor.IsOpen;

    public string ObjectsTabLabel => _localization.Format(
        "Coaching.MapControlEditor.Content.Tab.Objects",
        RuleCount);

    public string TimedRemindersTabLabel => _localization.Format(
        "Coaching.MapControlEditor.Content.Tab.Timed",
        0);

    public string PersistentTipsTabLabel => _localization.Format(
        "Coaching.MapControlEditor.Content.Tab.Persistent",
        0);

    public string PatchKnowledgeNotice => _timingSet is null
        ? _localization.Get("Coaching.MapControlEditor.Content.CatalogUnavailable")
        : _localization.Format(
            _timingSet.IsUsingPreviousPatchData
                ? "Coaching.MapControlEditor.Content.CatalogPreviousPatch"
                : "Coaching.MapControlEditor.Content.CatalogCurrentPatch",
            _timingSet.RequestedPatch,
            _timingSet.VerifiedThroughPatch);

    public void SetPatch(string patch)
    {
        _timingSet = MapObjectiveTimingCatalog.TryGetForPatch(patch, out var timingSet)
            ? timingSet
            : null;
        RebuildObjectiveOptions();
        OnPropertyChanged(nameof(PatchKnowledgeNotice));
    }

    public void OpenObjectivePicker()
    {
        if (_timingSet is null)
        {
            return;
        }

        Editor.Close();
        IsObjectivePickerOpen = true;
    }

    public void CloseObjectivePicker() => IsObjectivePickerOpen = false;

    public void BeginAddRule(MapObjectiveKind objective)
    {
        if (_timingSet is null)
        {
            return;
        }

        IsObjectivePickerOpen = false;
        Editor.BeginNew(_timingSet.Get(objective));
    }

    public void BeginEditRule(MapControlObjectiveRuleViewModel rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (_timingSet is null)
        {
            return;
        }

        IsObjectivePickerOpen = false;
        Editor.BeginEdit(rule.Rule, _timingSet.Get(rule.Rule.Objective));
    }

    public bool SaveEditor()
    {
        if (!Editor.CanSave)
        {
            return false;
        }

        if (Editor.EditingRuleId is { } editingRuleId)
        {
            var existing = _rules.FirstOrDefault(rule => rule.Id == editingRuleId);
            if (existing is null)
            {
                return false;
            }

            var rule = Editor.CreateRule(editingRuleId);
            MapObjectiveReminderRuleValidator.Validate(
                rule,
                _timingSet!.Get(rule.Objective));
            existing.Replace(rule);
        }
        else
        {
            var rule = Editor.CreateRule(CreateRuleId());
            MapObjectiveReminderRuleValidator.Validate(
                rule,
                _timingSet!.Get(rule.Objective));
            SubscribeAndAdd(new MapControlObjectiveRuleViewModel(rule, _localization));
        }

        Editor.Close();
        NotifyRulesChanged();
        return true;
    }

    public void CancelEditor() => Editor.Close();

    public bool DuplicateEditor()
    {
        if (!Editor.CanSave)
        {
            return false;
        }

        var sourceId = Editor.EditingRuleId;
        var sourceIndex = sourceId is null
            ? _rules.Count - 1
            : FindRuleIndex(sourceId);
        if (sourceId is null)
        {
            var created = Editor.CreateRule(CreateRuleId());
            var createdViewModel = new MapControlObjectiveRuleViewModel(created, _localization);
            SubscribeAndAdd(createdViewModel);
            sourceIndex = _rules.Count - 1;
        }
        else if (sourceIndex < 0)
        {
            return false;
        }

        var duplicate = Editor.CreateRule(CreateRuleId());
        var duplicateViewModel = new MapControlObjectiveRuleViewModel(duplicate, _localization);
        duplicateViewModel.PropertyChanged += Rule_PropertyChanged;
        _rules.Insert(sourceIndex + 1, duplicateViewModel);
        Editor.BeginEdit(duplicate, _timingSet!.Get(duplicate.Objective));
        NotifyRulesChanged();
        return true;
    }

    public bool DeleteEditor()
    {
        if (Editor.EditingRuleId is not { } id)
        {
            Editor.Close();
            return false;
        }

        var index = FindRuleIndex(id);
        if (index < 0)
        {
            return false;
        }

        _rules[index].PropertyChanged -= Rule_PropertyChanged;
        _rules.RemoveAt(index);
        Editor.Close();
        NotifyRulesChanged();
        return true;
    }

    public bool DuplicateRule(MapControlObjectiveRuleViewModel source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var index = _rules.IndexOf(source);
        if (index < 0)
        {
            return false;
        }

        var duplicate = source.Rule with { Id = CreateRuleId() };
        var viewModel = new MapControlObjectiveRuleViewModel(duplicate, _localization);
        viewModel.PropertyChanged += Rule_PropertyChanged;
        _rules.Insert(index + 1, viewModel);
        NotifyRulesChanged();
        return true;
    }

    public bool DeleteRule(MapControlObjectiveRuleViewModel rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!_rules.Remove(rule))
        {
            return false;
        }

        rule.PropertyChanged -= Rule_PropertyChanged;
        NotifyRulesChanged();
        return true;
    }

    public bool MoveRule(MapControlObjectiveRuleViewModel rule, int offset)
    {
        ArgumentNullException.ThrowIfNull(rule);
        var currentIndex = _rules.IndexOf(rule);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _rules.Count)
        {
            return false;
        }

        _rules.Move(currentIndex, targetIndex);
        return true;
    }

    public void BeginNew()
    {
        Editor.Close();
        IsObjectivePickerOpen = false;
        foreach (var rule in _rules)
        {
            rule.PropertyChanged -= Rule_PropertyChanged;
        }

        _rules.Clear();
        _nextRuleNumber = 1;
        NotifyRulesChanged();
    }

    public void UpdateLanguage()
    {
        RebuildObjectiveOptions();
        Editor.UpdateLanguage();
        foreach (var rule in _rules)
        {
            rule.UpdateLanguage();
        }

        OnPropertyChanged(nameof(ObjectsTabLabel));
        OnPropertyChanged(nameof(TimedRemindersTabLabel));
        OnPropertyChanged(nameof(PersistentTipsTabLabel));
        OnPropertyChanged(nameof(PatchKnowledgeNotice));
    }

    private void RebuildObjectiveOptions()
    {
        ObjectiveOptions = _timingSet?.Objectives.Select(timing =>
            new MapControlObjectiveOptionViewModel(
                timing.Objective,
                MapControlContentText.ObjectiveName(timing.Objective, _localization),
                _localization.Get(timing.Respawns
                    ? "Coaching.MapControlEditor.Content.Objective.Respawns"
                    : "Coaching.MapControlEditor.Content.Objective.Once"),
                _localization.Format(
                    timing.Respawns
                        ? "Coaching.MapControlEditor.Content.Objective.TimingRespawns"
                        : "Coaching.MapControlEditor.Content.Objective.TimingOnce",
                    MapControlContentText.FormatTime(timing.InitialSpawnTime),
                    timing.RespawnDelay is { } delay
                        ? MapControlContentText.FormatTime(delay)
                        : string.Empty),
                timing.Respawns))
            .ToArray() ?? [];
    }

    private string CreateRuleId()
    {
        string id;
        do
        {
            id = $"objective-rule-{_nextRuleNumber++}";
        }
        while (_rules.Any(rule => rule.Id == id));

        return id;
    }

    private int FindRuleIndex(string id) =>
        _rules.Select((rule, index) => (rule, index))
            .FirstOrDefault(pair => pair.rule.Id == id, (rule: null!, index: -1))
            .index;

    private void SubscribeAndAdd(MapControlObjectiveRuleViewModel rule)
    {
        rule.PropertyChanged += Rule_PropertyChanged;
        _rules.Add(rule);
    }

    private void Rule_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlObjectiveRuleViewModel.Rule))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MapControlObjectiveRuleEditorViewModel.IsOpen)
            or nameof(MapControlObjectiveRuleEditorViewModel.CanSave))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private void NotifyRulesChanged()
    {
        OnPropertyChanged(nameof(RuleCount));
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(ObjectsTabLabel));
    }
}
