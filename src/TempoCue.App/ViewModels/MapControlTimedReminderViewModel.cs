using TempoCue.App.Localization;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Представляет сохранённое напоминание по игровому времени.</summary>
public sealed class MapControlTimedReminderViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private MapControlTimedReminder _reminder;

    public MapControlTimedReminderViewModel(
        MapControlTimedReminder reminder,
        LocalizationService localization)
    {
        _reminder = reminder ?? throw new ArgumentNullException(nameof(reminder));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public string Id => _reminder.Id;

    public TimeSpan GameTime => _reminder.GameTime;

    public string GameTimeLabel => MapControlContentText.FormatTime(GameTime);

    public MapObjectiveReminderPriority Priority => _reminder.Priority;

    public string PriorityName => MapControlContentText.PriorityName(Priority, _localization);

    public string Title => _reminder.Title;

    public string Message => _reminder.Message;

    public string WhyNow => _reminder.WhyNow;

    public string NextGoal => _reminder.NextGoal;

    public bool HasNextGoal => !string.IsNullOrWhiteSpace(NextGoal);

    internal MapControlTimedReminder Reminder => _reminder;

    internal void Replace(MapControlTimedReminder reminder)
    {
        _reminder = reminder ?? throw new ArgumentNullException(nameof(reminder));
        OnPropertyChanged(string.Empty);
    }

    internal void UpdateLanguage()
    {
        OnPropertyChanged(nameof(PriorityName));
    }
}
