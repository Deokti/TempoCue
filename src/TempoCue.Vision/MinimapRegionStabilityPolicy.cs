namespace TempoCue.Vision;

/// <summary>Принимать ли новую находку области миникарты вместо установленной.</summary>
/// <remarks>
/// Размер миникарты в матче не меняется сам по себе: его задаёт настройка игры, и
/// поменять её посреди боя можно, но случается это редко. Зато одиночная неверная
/// находка случалась — TC-012: детектор вернул кусок карты, тот прошёл нижнюю
/// границу размера и на две секунды стал рабочей областью.
///
/// Отсюда правило: **сменить установленную область может только подтверждённая
/// находка**. Первая непохожая запоминается кандидатом, и лишь когда следующая
/// подтверждает её, область меняется. Одиночная ошибка отсекается целиком, а
/// настоящая смена размера стоит одного лишнего пересчёта.
///
/// Порог различия намеренно широкий. Задача не в том, чтобы ловить дрожание
/// границы на пиксель — оно безвредно, — а в том, чтобы не пропустить подмену
/// области её куском.
/// </remarks>
internal static class MinimapRegionStabilityPolicy
{
    /// <summary>
    /// Насколько сторона новой области может отличаться от установленной, чтобы
    /// считаться той же самой. Дрожание границы измеряется единицами пикселей,
    /// а подмена куском — десятками процентов; порог поставлен между ними.
    /// </summary>
    private const double SameRegionTolerance = 0.15;

    /// <summary>Что делать с новой находкой.</summary>
    internal enum Decision
    {
        /// <summary>Принять: область установлена или подтверждена.</summary>
        Accept,

        /// <summary>Запомнить кандидатом и оставить прежнюю область.</summary>
        Hold,

        /// <summary>Отбросить: находка не похожа ни на установленную, ни на кандидата.</summary>
        Reject
    }

    /// <param name="settled">Установленная область; null — её ещё нет.</param>
    /// <param name="candidate">Непринятая находка прошлого пересчёта; null — её нет.</param>
    /// <param name="found">Что нашли сейчас.</param>
    public static Decision Decide(
        PixelRegion? settled,
        PixelRegion? candidate,
        PixelRegion found)
    {
        // Установленной области нет — принимаем первое, что нашли: иначе
        // распознавание не начнётся никогда.
        if (settled is not { } current)
        {
            return Decision.Accept;
        }

        if (IsSameRegion(current, found))
        {
            return Decision.Accept;
        }

        // Та же непохожая область второй раз подряд — значит размер миникарты
        // действительно сменился, а не почудился.
        if (candidate is { } waiting && IsSameRegion(waiting, found))
        {
            return Decision.Accept;
        }

        return candidate is null ? Decision.Hold : Decision.Reject;
    }

    /// <summary>
    /// Одна ли это область. Сравнивается сторона, а не положение: миникарта стоит
    /// в углу экрана, и её угол при смене размера сдвигается вместе со стороной.
    /// </summary>
    private static bool IsSameRegion(PixelRegion first, PixelRegion second)
    {
        var larger = Math.Max(first.Width, second.Width);
        if (larger <= 0)
        {
            return false;
        }

        return Math.Abs(first.Width - second.Width) / (double)larger <= SameRegionTolerance;
    }
}
