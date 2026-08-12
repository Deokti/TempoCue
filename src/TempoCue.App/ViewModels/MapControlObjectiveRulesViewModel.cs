using System.Collections.ObjectModel;
using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Управляет правилами нейтральных объектов и их редактором.</summary>
public sealed class MapControlObjectiveRulesViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ObservableCollection<MapControlObjectiveRuleViewModel> _rules = [];
    private IReadOnlyList<MapControlObjectiveOptionViewModel> _objectiveOptions = [];
    private MapObjectiveTimingSet? _timingSet;
    private bool _isObjectivePickerOpen;
    private int _nextRuleNumber = 1;

    public MapControlObjectiveRulesViewModel(LocalizationService localization)
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

    public bool IsComplete => !Editor.IsOpen;

    public string RulesDescription => _timingSet is null
        ? _localization.Get("Coaching.MapControlEditor.Content.CatalogUnavailable")
        : _localization.Format(
            _timingSet.IsUsingPreviousPatchData
                ? "Coaching.MapControlEditor.Content.RulesDescriptionPreviousPatch"
                : "Coaching.MapControlEditor.Content.RulesDescriptionCurrentPatch",
            _timingSet.RequestedPatch,
            _timingSet.VerifiedThroughPatch);

    public void SetPatch(string patch)
    {
        _timingSet = MapObjectiveTimingCatalog.TryGetForPatch(patch, out var timingSet)
            ? timingSet
            : null;
        RebuildObjectiveOptions();
        OnPropertyChanged(nameof(RulesDescription));
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
        if (!Editor.CanSave || _timingSet is null)
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
            MapObjectiveReminderRuleValidator.Validate(rule, _timingSet.Get(rule.Objective));
            existing.Replace(rule);
        }
        else
        {
            var rule = Editor.CreateRule(CreateRuleId());
            MapObjectiveReminderRuleValidator.Validate(rule, _timingSet.Get(rule.Objective));
            SubscribeAndAdd(new MapControlObjectiveRuleViewModel(rule, _localization));
        }

        Editor.Close();
        NotifyRulesChanged();
        return true;
    }

    public void CancelEditor() => Editor.Close();

    public bool DuplicateEditor()
    {
        if (!Editor.CanSave || _timingSet is null)
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
            SubscribeAndAdd(new MapControlObjectiveRuleViewModel(created, _localization));
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
        Editor.BeginEdit(duplicate, _timingSet.Get(duplicate.Objective));
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

    public void Load(IEnumerable<MapObjectiveReminderRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        BeginNew();
        foreach (var rule in rules)
        {
            SubscribeAndAdd(new MapControlObjectiveRuleViewModel(rule, _localization));
        }

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

        OnPropertyChanged(nameof(RulesDescription));
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

    private int FindRuleIndex(string id)
    {
        for (var index = 0; index < _rules.Count; index++)
        {
            if (_rules[index].Id == id)
            {
                return index;
            }
        }

        return -1;
    }

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
    }
}
