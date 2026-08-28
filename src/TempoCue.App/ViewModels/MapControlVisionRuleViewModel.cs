using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Представляет сохранённое правило наблюдения за картой.</summary>
public sealed class MapControlVisionRuleViewModel : ObservableObject
{
    private readonly LocalizationService _localization;
    private MapVisionRule _rule;

    public MapControlVisionRuleViewModel(
        MapVisionRule rule,
        LocalizationService localization)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public string Id => _rule.Id;

    public PlayerPosition Position => _rule.Position;

    public MapVisionTransition Transition => _rule.Transition;

    /// <summary>
    /// Подпись карточки: «Мид · пропал из вида». Пара «роль и событие» — это и
    /// есть всё условие правила, поэтому она заменяет заголовок, которого у
    /// правила наблюдения нет.
    /// </summary>
    public string PairLabel => MapControlContentText.VisionPair(
        Position,
        Transition,
        _localization);

    public MapObjectiveReminderPriority Priority => _rule.Priority;

    public string PriorityName => MapControlContentText.PriorityName(Priority, _localization);

    public string Message => _rule.Message;

    public string WhyNow => _rule.WhyNow;

    public string NextGoal => _rule.NextGoal;

    public bool IsEnabled => _rule.IsEnabled;

    internal MapVisionRule Rule => _rule;

    internal void Replace(MapVisionRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        OnPropertyChanged(string.Empty);
    }

    internal void UpdateLanguage()
    {
        OnPropertyChanged(nameof(PairLabel));
        OnPropertyChanged(nameof(PriorityName));
    }
}
