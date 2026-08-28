using System.Collections.ObjectModel;
using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Владеет правилами наблюдения и их изолированным редактором.</summary>
/// <remarks>
/// Порядок правил авторский и не сортируется: при наблюдении срабатывает первое
/// подошедшее правило, поэтому порядок — часть замысла автора, а не оформление.
/// </remarks>
public sealed class MapControlVisionRulesViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ObservableCollection<MapControlVisionRuleViewModel> _rules = [];
    private int _nextRuleNumber = 1;

    public MapControlVisionRulesViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Rules = new ReadOnlyObservableCollection<MapControlVisionRuleViewModel>(_rules);
        Editor = new MapControlVisionRuleEditorViewModel(localization);
        Editor.PropertyChanged += Editor_PropertyChanged;
    }

    public ReadOnlyObservableCollection<MapControlVisionRuleViewModel> Rules { get; }

    public MapControlVisionRuleEditorViewModel Editor { get; }

    public bool HasRules => Count > 0;

    public int Count => _rules.Count;

    public bool IsComplete => !Editor.IsOpen;

    public IReadOnlyList<MapVisionRule> Snapshot =>
        _rules.Select(static rule => rule.Rule).ToArray();

    /// <summary>
    /// Пары «роль и событие», описанные больше одного раза. Сработает только
    /// первая, поэтому остальные — молчаливо мёртвый текст, и проверка набора
    /// обязана назвать их до сохранения.
    /// </summary>
    public IReadOnlyList<string> DuplicatePairLabels =>
        _rules
            .GroupBy(static rule => (rule.Position, rule.Transition))
            .Where(static group => group.Count() > 1)
            .Select(static group => group.First().PairLabel)
            .ToArray();

    public string EditorProgressLabel
    {
        get
        {
            var index = Editor.EditingRuleId is { } id ? FindIndex(id) + 1 : Count + 1;
            var total = Editor.EditingRuleId is null ? Count + 1 : Count;
            return _localization.Format(
                "Coaching.MapControlEditor.Content.Vision.Editor.Progress",
                Math.Max(1, index),
                Math.Max(1, total));
        }
    }

    public void BeginAdd()
    {
        Editor.BeginNew();
        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    public void BeginEdit(MapControlVisionRuleViewModel rule)
    {
        EnsureOwned(rule);
        Editor.BeginEdit(rule.Rule);
        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    public bool SaveEditor()
    {
        if (!Editor.CanSave)
        {
            return false;
        }

        if (Editor.EditingRuleId is { } id)
        {
            var existing = _rules.FirstOrDefault(rule => rule.Id == id);
            if (existing is null)
            {
                return false;
            }

            existing.Replace(Editor.CreateRule(id));
        }
        else
        {
            _rules.Add(new MapControlVisionRuleViewModel(
                Editor.CreateRule(CreateId()),
                _localization));
        }

        Editor.Close();
        NotifyCollectionChanged();
        return true;
    }

    public bool DuplicateEditor()
    {
        if (!Editor.CanSave)
        {
            return false;
        }

        if (Editor.EditingRuleId is null)
        {
            _rules.Add(new MapControlVisionRuleViewModel(
                Editor.CreateRule(CreateId()),
                _localization));
        }

        var duplicate = new MapControlVisionRuleViewModel(
            Editor.CreateRule(CreateId()),
            _localization);
        _rules.Add(duplicate);
        Editor.BeginEdit(duplicate.Rule);
        NotifyCollectionChanged();
        return true;
    }

    public MapControlVisionRuleViewModel Duplicate(MapControlVisionRuleViewModel source)
    {
        EnsureOwned(source);
        var duplicate = new MapControlVisionRuleViewModel(
            source.Rule with { Id = CreateId() },
            _localization);
        _rules.Insert(_rules.IndexOf(source) + 1, duplicate);
        NotifyCollectionChanged();
        return duplicate;
    }

    public bool DeleteEditor()
    {
        if (Editor.EditingRuleId is not { } id)
        {
            Editor.Close();
            return true;
        }

        var index = FindIndex(id);
        return index >= 0 && Delete(_rules[index]);
    }

    public bool Delete(MapControlVisionRuleViewModel rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!_rules.Remove(rule))
        {
            return false;
        }

        if (Editor.EditingRuleId == rule.Id)
        {
            Editor.Close();
        }

        NotifyCollectionChanged();
        return true;
    }

    public bool Move(MapControlVisionRuleViewModel rule, int offset)
    {
        EnsureOwned(rule);
        var current = _rules.IndexOf(rule);
        var target = current + offset;
        if (target < 0 || target >= _rules.Count)
        {
            return false;
        }

        _rules.Move(current, target);
        NotifyCollectionChanged();
        return true;
    }

    public void BeginNew()
    {
        Editor.Close();
        _rules.Clear();
        _nextRuleNumber = 1;
        NotifyCollectionChanged();
    }

    public void Load(IEnumerable<MapVisionRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        BeginNew();
        foreach (var rule in rules)
        {
            _rules.Add(new MapControlVisionRuleViewModel(rule, _localization));
        }

        NotifyCollectionChanged();
    }

    public void UpdateLanguage()
    {
        Editor.UpdateLanguage();
        foreach (var rule in _rules)
        {
            rule.UpdateLanguage();
        }

        OnPropertyChanged(nameof(EditorProgressLabel));
        OnPropertyChanged(nameof(DuplicatePairLabels));
    }

    private void EnsureOwned(MapControlVisionRuleViewModel rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (!_rules.Contains(rule))
        {
            throw new ArgumentException(
                "Правило наблюдения не принадлежит текущему черновику.",
                nameof(rule));
        }
    }

    private int FindIndex(string id)
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

    private string CreateId()
    {
        string id;
        do
        {
            id = $"vision-rule-{_nextRuleNumber++}";
        }
        while (_rules.Any(rule => rule.Id == id));

        return id;
    }

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlVisionRuleEditorViewModel.IsOpen))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private void NotifyCollectionChanged()
    {
        OnPropertyChanged(nameof(HasRules));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(EditorProgressLabel));
        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(IsComplete));
        OnPropertyChanged(nameof(DuplicatePairLabels));
    }
}
