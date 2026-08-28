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
            // Заголовок принадлежит приложению и не редактируется: он называет
            // роль и событие — то, что TempoCue знает наверняка. Автор набора
            // владеет советом под ним, а не фактом.
            localization.Get(ToHeadlineKey(rule.Position, rule.Transition)),
            FillChampionName(rule.Message, championName),
            localization.Get("Overlay.MapAwareness.Dismiss"),
            ToTone(rule.Priority),
            badge: localization.Get(ToPositionKey(rule.Position)));
    }

    /// <summary>
    /// Подставляет имя чемпиона в совет автора.
    /// </summary>
    /// <remarks>
    /// Метка заменяется, а не форматируется: в авторском тексте могут оказаться
    /// любые фигурные скобки, и string.Format на них падает. Совет без метки
    /// остаётся как есть — она необязательна.
    /// </remarks>
    public static string FillChampionName(string message, string championName) =>
        (message ?? string.Empty).Replace(
            ChampionPlaceholder,
            championName,
            StringComparison.OrdinalIgnoreCase);

    /// <summary>Метка имени чемпиона в тексте совета.</summary>
    public const string ChampionPlaceholder = "{champion}";

    private static string ToHeadlineKey(
        PlayerPosition position,
        MapVisionTransition transition)
    {
        var role = position switch
        {
            PlayerPosition.Top => "Top",
            PlayerPosition.Jungle => "Jungle",
            PlayerPosition.Middle => "Middle",
            PlayerPosition.Bottom => "Bottom",
            _ => "Support"
        };
        var evt = transition == MapVisionTransition.Appeared ? "Appeared" : "Disappeared";
        return $"Overlay.MapAwareness.Headline.{role}.{evt}";
    }

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
