using System.Collections.ObjectModel;
using TempoCue.App.Localization;

namespace TempoCue.App.ViewModels;

/// <summary>Владелец постоянных подсказок и их изолированного редактора.</summary>
public sealed class MatchupPersistentTipsViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ObservableCollection<MatchupPersistentTipViewModel> _tips = [];
    private IReadOnlyList<MatchupPhaseViewModel> _phases = [];
    private int _nextTipNumber = 1;

    public MatchupPersistentTipsViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Tips = new ReadOnlyObservableCollection<MatchupPersistentTipViewModel>(_tips);
        Editor = new MatchupPersistentTipEditorViewModel(localization);
    }

    public ReadOnlyObservableCollection<MatchupPersistentTipViewModel> Tips { get; }

    public MatchupPersistentTipEditorViewModel Editor { get; }

    public bool HasTips => _tips.Count > 0;

    public int Count => _tips.Count;

    public string EditorProgressLabel
    {
        get
        {
            var index = Editor.EditingTipId is string editingTipId
                ? FindTipIndex(editingTipId) + 1
                : Count + 1;
            var total = Editor.EditingTipId is null ? Count + 1 : Count;
            return _localization.Format(
                "Coaching.MatchupEditor.Persistent.Editor.Progress",
                Math.Max(1, index),
                Math.Max(1, total));
        }
    }

    public void BeginAdd()
    {
        Editor.BeginNew();
        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    public void BeginEdit(MatchupPersistentTipViewModel tip)
    {
        EnsureOwnedTip(tip);
        Editor.BeginEdit(tip.CreateSnapshot());
        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    public bool SaveEditor()
    {
        if (!Editor.CanSave)
        {
            return false;
        }

        if (Editor.EditingTipId is string editingTipId)
        {
            var existingTip = _tips.FirstOrDefault(tip => tip.Id == editingTipId);
            if (existingTip is null)
            {
                return false;
            }

            existingTip.Replace(Editor.CreateSnapshot(editingTipId));
            existingTip.UpdateScope(_phases);
        }
        else
        {
            var tip = new MatchupPersistentTipViewModel(
                Editor.CreateSnapshot(CreateTipId()),
                _localization);
            tip.UpdateScope(_phases);
            _tips.Add(tip);
        }

        Editor.Close();
        NotifyCollectionChanged();
        return true;
    }

    public MatchupPersistentTipViewModel? DuplicateEditor()
    {
        if (!Editor.CanSave)
        {
            return null;
        }

        MatchupPersistentTipDraft source;
        var sourceIndex = _tips.Count - 1;
        if (Editor.EditingTipId is string editingTipId)
        {
            sourceIndex = FindTipIndex(editingTipId);
            if (sourceIndex < 0)
            {
                return null;
            }

            source = Editor.CreateSnapshot(editingTipId);
        }
        else
        {
            source = Editor.CreateSnapshot(CreateTipId());
            var original = new MatchupPersistentTipViewModel(source, _localization);
            original.UpdateScope(_phases);
            _tips.Add(original);
            sourceIndex = _tips.Count - 1;
        }

        var copy = CreateDuplicate(source);
        _tips.Insert(sourceIndex + 1, copy);
        Editor.BeginEdit(copy.CreateSnapshot());
        NotifyCollectionChanged();
        return copy;
    }

    public bool DeleteEditor()
    {
        if (Editor.EditingTipId is not string editingTipId)
        {
            Editor.Close();
            return true;
        }

        var tipIndex = FindTipIndex(editingTipId);
        return tipIndex >= 0 && Delete(_tips[tipIndex]);
    }

    public MatchupPersistentTipViewModel Duplicate(MatchupPersistentTipViewModel tip)
    {
        var sourceIndex = EnsureOwnedTip(tip);
        var copy = CreateDuplicate(tip.CreateSnapshot());
        _tips.Insert(sourceIndex + 1, copy);
        NotifyCollectionChanged();
        return copy;
    }

    public bool Delete(MatchupPersistentTipViewModel tip)
    {
        ArgumentNullException.ThrowIfNull(tip);
        var removed = _tips.Remove(tip);
        if (!removed)
        {
            return false;
        }

        if (Editor.EditingTipId == tip.Id)
        {
            Editor.Close();
        }

        NotifyCollectionChanged();
        return true;
    }

    public void UpdatePhases(IEnumerable<MatchupPhaseViewModel> phases)
    {
        ArgumentNullException.ThrowIfNull(phases);
        _phases = phases.ToArray();
        Editor.UpdatePhaseOptions(_phases);
        foreach (var tip in _tips)
        {
            tip.UpdateScope(_phases);
        }
    }

    public void UpdateLanguage()
    {
        Editor.UpdateLanguage(_phases);
        OnPropertyChanged(nameof(EditorProgressLabel));
        foreach (var tip in _tips)
        {
            tip.UpdateScope(_phases);
        }
    }

    public void Load(IEnumerable<MatchupPersistentTipDraft> tips)
    {
        ArgumentNullException.ThrowIfNull(tips);
        Editor.Close();
        _tips.Clear();
        foreach (var draft in tips)
        {
            _tips.Add(new MatchupPersistentTipViewModel(draft, _localization));
        }

        _nextTipNumber = 1;
        UpdatePhases(_phases);
        NotifyCollectionChanged();
    }

    private MatchupPersistentTipViewModel CreateDuplicate(MatchupPersistentTipDraft source)
    {
        var duplicateTitle = _localization.Format(
            "Coaching.MatchupEditor.Content.DuplicateName",
            source.Title);
        var copy = new MatchupPersistentTipViewModel(
            source with
            {
                Id = CreateTipId(),
                Title = duplicateTitle.Length <= 80
                    ? duplicateTitle
                    : duplicateTitle[..80].TrimEnd()
            },
            _localization);
        copy.UpdateScope(_phases);
        return copy;
    }

    private int EnsureOwnedTip(MatchupPersistentTipViewModel tip)
    {
        ArgumentNullException.ThrowIfNull(tip);
        var index = _tips.IndexOf(tip);
        if (index < 0)
        {
            throw new ArgumentException("Подсказка не принадлежит текущему черновику.", nameof(tip));
        }

        return index;
    }

    private int FindTipIndex(string tipId)
    {
        for (var index = 0; index < _tips.Count; index++)
        {
            if (_tips[index].Id == tipId)
            {
                return index;
            }
        }

        return -1;
    }

    private string CreateTipId()
    {
        string id;
        do
        {
            id = $"persistent-tip-{_nextTipNumber++}";
        }
        while (_tips.Any(tip => tip.Id == id));

        return id;
    }

    private void NotifyCollectionChanged()
    {
        OnPropertyChanged(nameof(HasTips));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(EditorProgressLabel));
    }
}
