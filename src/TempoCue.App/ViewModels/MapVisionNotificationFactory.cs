using TempoCue.App.Localization;
using TempoCue.Coaching;
using TempoCue.Core;
using TempoCue.Overlay;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Собирает игровое уведомление по правилу наблюдения.
/// </summary>
/// <remarks>
/// Одна фабрика на бой и на предпросмотр в редакторе. Собрать уведомление
/// вторым способом означало бы показывать автору не то, что увидит игрок, —
/// и расхождение проявилось бы не сразу, а через правку в одном из двух мест.
///
/// Текст принадлежит набору, а не приложению: локализованными остаются только
/// оформление окна и название роли — то, что относится к самому TempoCue.
/// </remarks>
internal static class MapVisionNotificationFactory
{
    public static OverlayNotification Create(
        MapVisionRule rule,
        string championName,
        LocalizationService localization,
        string? idSuffix = null)
    {
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(localization);
        return new OverlayNotification(
            $"map-awareness:{rule.Id}:{idSuffix ?? championName}",
            localization.Get("Overlay.MapAwareness.Status"),
            // Главной фразой стоит само событие: «Треш пропал из вида». Одного
            // имени чемпиона мало — игрок видит, о ком речь, но не видит, что
            // случилось, а текст правила пишет автор и назвать событие не обязан.
            localization.Format(
                ToEventKey(rule.Transition),
                championName),
            rule.Message,
            localization.Get("Overlay.MapAwareness.Dismiss"),
            ToTone(rule.Priority),
            badge: localization.Get(ToPositionKey(rule.Position)));
    }

    private static string ToEventKey(MapVisionTransition transition) =>
        transition == MapVisionTransition.Appeared
            ? "Overlay.MapAwareness.Event.Appeared"
            : "Overlay.MapAwareness.Event.Disappeared";

    private static OverlayNotificationTone ToTone(MapObjectiveReminderPriority priority) =>
        priority switch
        {
            MapObjectiveReminderPriority.Important => OverlayNotificationTone.Danger,
            MapObjectiveReminderPriority.Caution => OverlayNotificationTone.Caution,
            _ => OverlayNotificationTone.Coach
        };

    private static string ToPositionKey(PlayerPosition position) => position switch
    {
        PlayerPosition.Top => "Position.Top",
        PlayerPosition.Jungle => "Position.Jungle",
        PlayerPosition.Middle => "Position.Middle",
        PlayerPosition.Bottom => "Position.Bottom",
        PlayerPosition.Support => "Position.Support",
        _ => "Position.Unknown"
    };
}
