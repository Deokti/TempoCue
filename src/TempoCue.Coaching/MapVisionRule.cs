namespace TempoCue.Coaching;

/// <summary>Наблюдаемое изменение видимости соперника на миникарте.</summary>
public enum MapVisionTransition
{
    /// <summary>Соперник появился в поле зрения.</summary>
    Appeared,

    /// <summary>Соперник пропал из поля зрения.</summary>
    Disappeared
}

/// <summary>
/// Правило уведомления по наблюдению на миникарте.
/// </summary>
/// <remarks>
/// Отличается от <see cref="MapObjectiveReminderRule"/> тем, что запускает его:
/// правило объекта срабатывает по игровому времени и заранее, с упреждением, а
/// это — по факту наблюдения. Поэтому у него нет ни объекта, ни упреждения, а
/// есть роль соперника и событие. Обоснование раздельных типов — ADR 0016.
///
/// Формат описывает всю матрицу «пять ролей на два события». Встроенных наборов
/// поставляется два, но правило для любой другой роли работает без изменений в
/// коде: приложение не знает наперёд, какие сигналы существуют.
/// </remarks>
public sealed record MapVisionRule(
    string Id,
    PlayerPosition Position,
    MapVisionTransition Transition,
    MapObjectiveReminderPriority Priority,
    string Message,
    string WhyNow,
    string NextGoal,
    bool IsEnabled = true);
