using TempoCue.App.Localization;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Представляет сохранённое правило объекта и его локализованное резюме.</summary>
public sealed class MapControlObjectiveRuleViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private MapObjectiveReminderRule _rule;

    public MapControlObjectiveRuleViewModel(
        MapObjectiveReminderRule rule,
        LocalizationService localization)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public string Id => _rule.Id;

    public MapObjectiveReminderRule Rule => _rule;

    public string ObjectiveName => MapControlContentText.ObjectiveName(_rule.Objective, _localization);

    public MapObjectiveReminderPriority Priority => _rule.Priority;

    public string PriorityName => MapControlContentText.PriorityName(_rule.Priority, _localization);

    public string RecurrenceName => MapControlContentText.RecurrenceName(_rule.Recurrence, _localization);

    public string TriggerSummary => MapControlContentText.TriggerSummary(_rule, _localization);

    public string Message => _rule.Message;

    public string WhyNow => _rule.WhyNow;

    public string NextGoal => _rule.NextGoal;

    public bool IsEnabled
    {
        get => _rule.IsEnabled;
        set
        {
            if (_rule.IsEnabled == value)
            {
                return;
            }

            _rule = _rule with { IsEnabled = value };
            OnPropertyChanged();
            OnPropertyChanged(nameof(Rule));
        }
    }

    public void Replace(MapObjectiveReminderRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        NotifyAll();
    }

    public void UpdateLanguage() => NotifyAll();

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(Rule));
        OnPropertyChanged(nameof(ObjectiveName));
        OnPropertyChanged(nameof(Priority));
        OnPropertyChanged(nameof(PriorityName));
        OnPropertyChanged(nameof(RecurrenceName));
        OnPropertyChanged(nameof(TriggerSummary));
        OnPropertyChanged(nameof(Message));
        OnPropertyChanged(nameof(WhyNow));
        OnPropertyChanged(nameof(NextGoal));
        OnPropertyChanged(nameof(IsEnabled));
    }
}
