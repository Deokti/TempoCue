using TempoCue.Core;

namespace TempoCue.App.ViewModels;

public sealed record MapControlObjectiveOptionViewModel(
    MapObjectiveKind Objective,
    string DisplayName,
    string Description,
    string TimingSummary,
    bool Respawns);

public sealed record MapControlRuleTriggerOptionViewModel(
    TempoCue.Coaching.MapObjectiveReminderTrigger Value,
    string DisplayName,
    string Description,
    bool IsEnabled)
{
    public override string ToString() => DisplayName;
}

public sealed record MapControlRuleRecurrenceOptionViewModel(
    TempoCue.Coaching.MapObjectiveReminderRecurrence Value,
    string DisplayName,
    string Description,
    bool IsEnabled)
{
    public override string ToString() => DisplayName;
}

public sealed record MapControlRulePriorityOptionViewModel(
    TempoCue.Coaching.MapObjectiveReminderPriority Value,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}
