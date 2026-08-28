using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

internal static class MapControlContentText
{
    public static string ObjectiveName(MapObjectiveKind objective, LocalizationService localization) =>
        localization.Get(objective switch
        {
            MapObjectiveKind.ElementalDragon => "Coaching.MapControlEditor.Content.Objective.Dragon",
            MapObjectiveKind.VoidGrubs => "Coaching.MapControlEditor.Content.Objective.Grubs",
            MapObjectiveKind.RiftHerald => "Coaching.MapControlEditor.Content.Objective.Herald",
            MapObjectiveKind.BaronNashor => "Coaching.MapControlEditor.Content.Objective.Baron",
            _ => throw new ArgumentOutOfRangeException(nameof(objective), objective, null)
        });

    /// <summary>
    /// Роль в области применения набора. Не переводится: TOP, JUNGLE, MID, ADC и
    /// SUPPORT — это имена самой игры, и игрок знает их именно такими.
    /// </summary>
    public static string RoleName(MapControlRole role) => role switch
    {
        MapControlRole.Top => "TOP",
        MapControlRole.Jungle => "JUNGLE",
        MapControlRole.Mid => "MID",
        MapControlRole.Adc => "ADC",
        MapControlRole.Support => "SUPPORT",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    /// <summary>
    /// Роль соперника в правиле наблюдения. Обязана совпадать с
    /// <see cref="RoleName"/>: игрок выбирает одни и те же пять ролей на двух
    /// шагах подряд, и разные названия читались бы как разные вещи. Перечисления
    /// при этом разные — область применения живёт в наборах, а наблюдение в
    /// составе матча, — поэтому имена стоят рядом, чтобы расхождение было видно.
    /// </summary>
    public static string VisionPositionName(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => "TOP",
        PlayerPosition.Jungle => "JUNGLE",
        PlayerPosition.Middle => "MID",
        PlayerPosition.Bottom => "ADC",
        PlayerPosition.Support => "SUPPORT",
        _ => throw new ArgumentOutOfRangeException(nameof(position), position, null)
    };

    public static string VisionTransitionName(
        MapVisionTransition transition,
        LocalizationService localization) =>
        localization.Get(transition switch
        {
            MapVisionTransition.Appeared => "Coaching.MapControlEditor.Content.Vision.Transition.Appeared",
            MapVisionTransition.Disappeared => "Coaching.MapControlEditor.Content.Vision.Transition.Disappeared",
            _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null)
        });

    /// <summary>Подпись пары условия: «Мид · пропал из вида».</summary>
    public static string VisionPair(
        PlayerPosition position,
        MapVisionTransition transition,
        LocalizationService localization) =>
        $"{VisionPositionName(position)} · {VisionTransitionName(transition, localization)}";

    public static string PriorityName(
        MapObjectiveReminderPriority priority,
        LocalizationService localization) =>
        localization.Get(priority switch
        {
            MapObjectiveReminderPriority.Information => "Coaching.MapControlEditor.Content.Priority.Information",
            MapObjectiveReminderPriority.Caution => "Coaching.MapControlEditor.Content.Priority.Caution",
            MapObjectiveReminderPriority.Important => "Coaching.MapControlEditor.Content.Priority.Important",
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, null)
        });

    public static string RecurrenceName(
        MapObjectiveReminderRecurrence recurrence,
        LocalizationService localization) =>
        localization.Get(recurrence switch
        {
            MapObjectiveReminderRecurrence.OncePerMatch => "Coaching.MapControlEditor.Content.Recurrence.Once",
            MapObjectiveReminderRecurrence.EveryCycle => "Coaching.MapControlEditor.Content.Recurrence.EveryCycle",
            _ => throw new ArgumentOutOfRangeException(nameof(recurrence), recurrence, null)
        });

    public static string RecurrenceDescription(
        MapObjectiveReminderRecurrence recurrence,
        MapObjectiveReminderTrigger trigger,
        bool canRepeat,
        LocalizationService localization) =>
        localization.Get(recurrence switch
        {
            MapObjectiveReminderRecurrence.OncePerMatch =>
                "Coaching.MapControlEditor.Content.Recurrence.Once.Description",
            MapObjectiveReminderRecurrence.EveryCycle when !canRepeat =>
                "Coaching.MapControlEditor.Content.Recurrence.EveryCycle.Unavailable.Description",
            MapObjectiveReminderRecurrence.EveryCycle when trigger is
                MapObjectiveReminderTrigger.BeforeInitialSpawn or
                MapObjectiveReminderTrigger.AtInitialSpawn =>
                "Coaching.MapControlEditor.Content.Recurrence.EveryCycle.Initial.Description",
            MapObjectiveReminderRecurrence.EveryCycle when trigger == MapObjectiveReminderTrigger.BeforeRespawn =>
                "Coaching.MapControlEditor.Content.Recurrence.EveryCycle.Respawn.Description",
            MapObjectiveReminderRecurrence.EveryCycle =>
                "Coaching.MapControlEditor.Content.Recurrence.EveryCycle.AfterKill.Description",
            _ => throw new ArgumentOutOfRangeException(nameof(recurrence), recurrence, null)
        });

    public static string TriggerName(
        MapObjectiveReminderTrigger trigger,
        LocalizationService localization) =>
        localization.Get(trigger switch
        {
            MapObjectiveReminderTrigger.BeforeInitialSpawn => "Coaching.MapControlEditor.Content.Trigger.BeforeInitial",
            MapObjectiveReminderTrigger.AtInitialSpawn => "Coaching.MapControlEditor.Content.Trigger.AtInitial",
            MapObjectiveReminderTrigger.BeforeRespawn => "Coaching.MapControlEditor.Content.Trigger.BeforeRespawn",
            MapObjectiveReminderTrigger.AfterKill => "Coaching.MapControlEditor.Content.Trigger.AfterKill",
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null)
        });

    public static string TriggerDescription(
        MapObjectiveReminderTrigger trigger,
        LocalizationService localization) =>
        localization.Get(trigger switch
        {
            MapObjectiveReminderTrigger.BeforeInitialSpawn =>
                "Coaching.MapControlEditor.Content.Trigger.BeforeInitial.Description",
            MapObjectiveReminderTrigger.AtInitialSpawn =>
                "Coaching.MapControlEditor.Content.Trigger.AtInitial.Description",
            MapObjectiveReminderTrigger.BeforeRespawn =>
                "Coaching.MapControlEditor.Content.Trigger.BeforeRespawn.Description",
            MapObjectiveReminderTrigger.AfterKill =>
                "Coaching.MapControlEditor.Content.Trigger.AfterKill.Description",
            _ => throw new ArgumentOutOfRangeException(nameof(trigger), trigger, null)
        });

    public static string TriggerSummary(
        MapObjectiveReminderRule rule,
        LocalizationService localization) =>
        rule.LeadTime > TimeSpan.Zero
            ? localization.Format(
                "Coaching.MapControlEditor.Content.TriggerSummary.Before",
                (int)rule.LeadTime.TotalSeconds,
                TriggerName(rule.Trigger, localization).ToLowerInvariant())
            : TriggerName(rule.Trigger, localization);

    public static string PersistentTipScopeName(
        MapControlPersistentTipScope scope,
        LocalizationService localization) =>
        scope.Kind switch
        {
            MapControlPersistentTipScopeKind.WholeMatch => localization.Get(
                "Coaching.MapControlEditor.Content.Persistent.Scope.WholeMatch"),
            MapControlPersistentTipScopeKind.BeforeFirstObjective => localization.Get(
                "Coaching.MapControlEditor.Content.Persistent.Scope.BeforeFirstObjective"),
            MapControlPersistentTipScopeKind.WhileAnyObjectiveAvailable => localization.Get(
                "Coaching.MapControlEditor.Content.Persistent.Scope.WhileObjectiveAvailable"),
            MapControlPersistentTipScopeKind.SpecificObjective when scope.Objective is { } objective =>
                ObjectiveName(objective, localization),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null)
        };

    public static string FormatTime(TimeSpan value) =>
        $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
}
