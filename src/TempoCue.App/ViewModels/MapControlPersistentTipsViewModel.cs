using System.Collections.ObjectModel;
using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Владеет постоянными подсказками и их изолированным редактором.</summary>
public sealed class MapControlPersistentTipsViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ObservableCollection<MapControlPersistentTipViewModel> _tips = [];
    private int _nextTipNumber = 1;

    public MapControlPersistentTipsViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Tips = new ReadOnlyObservableCollection<MapControlPersistentTipViewModel>(_tips);
        Editor = new MapControlPersistentTipEditorViewModel(localization);
        Editor.PropertyChanged += Editor_PropertyChanged;
    }

    public ReadOnlyObservableCollection<MapControlPersistentTipViewModel> Tips { get; }

    public MapControlPersistentTipEditorViewModel Editor { get; }

    public bool HasTips => Count > 0;

    public int Count => _tips.Count;

    public bool IsComplete => !Editor.IsOpen;

    public IReadOnlyList<MapControlPersistentTip> Snapshot =>
        _tips.Select(static tip => tip.Tip).ToArray();

    public string EditorProgressLabel
    {
        get
        {
            var index = Editor.EditingTipId is { } id ? FindTipIndex(id) + 1 : Count + 1;
            var total = Editor.EditingTipId is null ? Count + 1 : Count;
            return _localization.Format(
                "Coaching.MapControlEditor.Content.Persistent.Editor.Progress",
                Math.Max(1, index),
                Math.Max(1, total));
        }
    }

    public void BeginAdd()
    {
        Editor.BeginNew();
        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    public void BeginEdit(MapControlPersistentTipViewModel tip)
    {
        EnsureOwnedTip(tip);
        Editor.BeginEdit(tip.Tip);
        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    public bool SaveEditor()
    {
        if (!Editor.CanSave)
        {
            return false;
        }

        if (Editor.EditingTipId is { } id)
        {
            var existing = _tips.FirstOrDefault(tip => tip.Id == id);
            if (existing is null)
            {
                return false;
            }

            existing.Replace(Editor.CreateTip(id));
        }
        else
        {
            _tips.Add(new MapControlPersistentTipViewModel(
                Editor.CreateTip(CreateTipId()),
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

        var sourceIndex = Editor.EditingTipId is { } id ? FindTipIndex(id) : _tips.Count - 1;
        if (Editor.EditingTipId is null)
        {
            _tips.Add(new MapControlPersistentTipViewModel(
                Editor.CreateTip(CreateTipId()),
                _localization));
            sourceIndex = _tips.Count - 1;
        }
        else if (sourceIndex < 0)
        {
            return false;
        }

        var duplicate = CreateDuplicate(Editor.CreateTip(CreateTipId()));
        _tips.Insert(sourceIndex + 1, duplicate);
        Editor.BeginEdit(duplicate.Tip);
        NotifyCollectionChanged();
        return true;
    }

    public MapControlPersistentTipViewModel Duplicate(MapControlPersistentTipViewModel source)
    {
        var sourceIndex = EnsureOwnedTip(source);
        var duplicate = CreateDuplicate(source.Tip with { Id = CreateTipId() });
        _tips.Insert(sourceIndex + 1, duplicate);
        NotifyCollectionChanged();
        return duplicate;
    }

    public bool DeleteEditor()
    {
        if (Editor.EditingTipId is not { } id)
        {
            Editor.Close();
            return true;
        }

        var index = FindTipIndex(id);
        return index >= 0 && Delete(_tips[index]);
    }

    public bool Delete(MapControlPersistentTipViewModel tip)
    {
        ArgumentNullException.ThrowIfNull(tip);
        if (!_tips.Remove(tip))
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

    public bool Move(MapControlPersistentTipViewModel tip, int offset)
    {
        var currentIndex = EnsureOwnedTip(tip);
        var targetIndex = currentIndex + offset;
        if (targetIndex < 0 || targetIndex >= _tips.Count)
        {
            return false;
        }

        _tips.Move(currentIndex, targetIndex);
        OnPropertyChanged(nameof(Snapshot));
        return true;
    }

    public void BeginNew()
    {
        Editor.Close();
        _tips.Clear();
        _nextTipNumber = 1;
        NotifyCollectionChanged();
    }

    public void UpdateLanguage()
    {
        Editor.UpdateLanguage();
        foreach (var tip in _tips)
        {
            tip.UpdateLanguage();
        }

        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    private MapControlPersistentTipViewModel CreateDuplicate(MapControlPersistentTip source)
    {
        var title = _localization.Format(
            "Coaching.MapControlEditor.Content.DuplicateName",
            source.Title);
        return new MapControlPersistentTipViewModel(
            source with
            {
                Title = title.Length <= 80 ? title : title[..80].TrimEnd()
            },
            _localization);
    }

    private int EnsureOwnedTip(MapControlPersistentTipViewModel tip)
    {
        ArgumentNullException.ThrowIfNull(tip);
        var index = _tips.IndexOf(tip);
        if (index < 0)
        {
            throw new ArgumentException(
                "Подсказка не принадлежит текущему черновику.",
                nameof(tip));
        }

        return index;
    }

    private int FindTipIndex(string id)
    {
        for (var index = 0; index < _tips.Count; index++)
        {
            if (_tips[index].Id == id)
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

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlPersistentTipEditorViewModel.IsOpen))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private void NotifyCollectionChanged()
    {
        OnPropertyChanged(nameof(HasTips));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(EditorProgressLabel));
        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(IsComplete));
    }
}
