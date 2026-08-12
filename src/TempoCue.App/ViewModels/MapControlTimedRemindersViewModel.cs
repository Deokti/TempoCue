using System.Collections.ObjectModel;
using System.ComponentModel;
using TempoCue.App.Localization;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Владеет напоминаниями по времени и их изолированным редактором.</summary>
public sealed class MapControlTimedRemindersViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private readonly ObservableCollection<MapControlTimedReminderViewModel> _reminders = [];
    private int _nextReminderNumber = 1;

    public MapControlTimedRemindersViewModel(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
        Reminders = new ReadOnlyObservableCollection<MapControlTimedReminderViewModel>(_reminders);
        Editor = new MapControlTimedReminderEditorViewModel(localization);
        Editor.PropertyChanged += Editor_PropertyChanged;
    }

    public ReadOnlyObservableCollection<MapControlTimedReminderViewModel> Reminders { get; }

    public MapControlTimedReminderEditorViewModel Editor { get; }

    public bool HasReminders => Count > 0;

    public int Count => _reminders.Count;

    public bool IsComplete => !Editor.IsOpen;

    public IReadOnlyList<MapControlTimedReminder> Snapshot =>
        _reminders.Select(static reminder => reminder.Reminder).ToArray();

    public string EditorProgressLabel
    {
        get
        {
            var index = Editor.EditingReminderId is { } id ? FindIndex(id) + 1 : Count + 1;
            var total = Editor.EditingReminderId is null ? Count + 1 : Count;
            return _localization.Format(
                "Coaching.MapControlEditor.Content.Timed.Editor.Progress",
                Math.Max(1, index),
                Math.Max(1, total));
        }
    }

    public void BeginAdd()
    {
        Editor.BeginNew();
        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    public void BeginEdit(MapControlTimedReminderViewModel reminder)
    {
        EnsureOwned(reminder);
        Editor.BeginEdit(reminder.Reminder);
        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    public bool SaveEditor()
    {
        if (!Editor.CanSave)
        {
            return false;
        }

        if (Editor.EditingReminderId is { } id)
        {
            var existing = _reminders.FirstOrDefault(reminder => reminder.Id == id);
            if (existing is null)
            {
                return false;
            }

            existing.Replace(Editor.CreateReminder(id));
        }
        else
        {
            _reminders.Add(new MapControlTimedReminderViewModel(
                Editor.CreateReminder(CreateId()),
                _localization));
        }

        SortByGameTime();
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

        if (Editor.EditingReminderId is null)
        {
            _reminders.Add(new MapControlTimedReminderViewModel(
                Editor.CreateReminder(CreateId()),
                _localization));
        }

        var duplicate = CreateDuplicate(Editor.CreateReminder(CreateId()));
        _reminders.Add(duplicate);
        SortByGameTime();
        Editor.BeginEdit(duplicate.Reminder);
        NotifyCollectionChanged();
        return true;
    }

    public MapControlTimedReminderViewModel Duplicate(MapControlTimedReminderViewModel source)
    {
        EnsureOwned(source);
        var duplicate = CreateDuplicate(source.Reminder with { Id = CreateId() });
        _reminders.Add(duplicate);
        SortByGameTime();
        NotifyCollectionChanged();
        return duplicate;
    }

    public bool DeleteEditor()
    {
        if (Editor.EditingReminderId is not { } id)
        {
            Editor.Close();
            return true;
        }

        var index = FindIndex(id);
        return index >= 0 && Delete(_reminders[index]);
    }

    public bool Delete(MapControlTimedReminderViewModel reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        if (!_reminders.Remove(reminder))
        {
            return false;
        }

        if (Editor.EditingReminderId == reminder.Id)
        {
            Editor.Close();
        }

        NotifyCollectionChanged();
        return true;
    }

    public void BeginNew()
    {
        Editor.Close();
        _reminders.Clear();
        _nextReminderNumber = 1;
        NotifyCollectionChanged();
    }

    public void UpdateLanguage()
    {
        Editor.UpdateLanguage();
        foreach (var reminder in _reminders)
        {
            reminder.UpdateLanguage();
        }

        OnPropertyChanged(nameof(EditorProgressLabel));
    }

    private MapControlTimedReminderViewModel CreateDuplicate(MapControlTimedReminder source)
    {
        var title = _localization.Format(
            "Coaching.MapControlEditor.Content.DuplicateName",
            source.Title);
        return new MapControlTimedReminderViewModel(
            source with { Title = title.Length <= 80 ? title : title[..80].TrimEnd() },
            _localization);
    }

    private void SortByGameTime()
    {
        var ordered = _reminders
            .OrderBy(static reminder => reminder.GameTime)
            .ThenBy(static reminder => reminder.Id, StringComparer.Ordinal)
            .ToArray();
        for (var target = 0; target < ordered.Length; target++)
        {
            var current = _reminders.IndexOf(ordered[target]);
            if (current != target)
            {
                _reminders.Move(current, target);
            }
        }
    }

    private void EnsureOwned(MapControlTimedReminderViewModel reminder)
    {
        ArgumentNullException.ThrowIfNull(reminder);
        if (!_reminders.Contains(reminder))
        {
            throw new ArgumentException(
                "Напоминание не принадлежит текущему черновику.",
                nameof(reminder));
        }
    }

    private int FindIndex(string id)
    {
        for (var index = 0; index < _reminders.Count; index++)
        {
            if (_reminders[index].Id == id)
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
            id = $"timed-reminder-{_nextReminderNumber++}";
        }
        while (_reminders.Any(reminder => reminder.Id == id));

        return id;
    }

    private void Editor_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapControlTimedReminderEditorViewModel.IsOpen))
        {
            OnPropertyChanged(nameof(IsComplete));
        }
    }

    private void NotifyCollectionChanged()
    {
        OnPropertyChanged(nameof(HasReminders));
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(EditorProgressLabel));
        OnPropertyChanged(nameof(Snapshot));
        OnPropertyChanged(nameof(IsComplete));
    }
}
