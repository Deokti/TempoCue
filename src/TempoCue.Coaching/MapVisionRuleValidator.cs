namespace TempoCue.Coaching;

/// <summary>Проверяет правило уведомления по наблюдению на миникарте.</summary>
public static class MapVisionRuleValidator
{
    public static void Validate(MapVisionRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);

        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            throw new InvalidDataException("Vision rule requires an identifier.");
        }

        // Unknown отвергается отдельно от неопределённых значений: это не
        // повреждённый файл, а правило «уведомлять о ком угодно». Роль участника
        // источник определяет не всегда, и такое правило срабатывало бы на
        // случайных людях вместо названного.
        if (rule.Position is PlayerPosition.Unknown || !Enum.IsDefined(rule.Position))
        {
            throw new InvalidDataException(
                $"Vision rule '{rule.Id}' must name a known player position.");
        }

        if (!Enum.IsDefined(rule.Transition))
        {
            throw new InvalidDataException(
                $"Vision rule '{rule.Id}' references an unsupported visibility transition.");
        }

        if (!Enum.IsDefined(rule.Priority))
        {
            throw new InvalidDataException(
                $"Vision rule '{rule.Id}' references an unsupported priority.");
        }

        // Обязателен только совет: без него игрок увидел бы пустое уведомление.
        // «Почему сейчас» и «что дальше» необязательны — в оверлей они не
        // попадают вовсе и живут в карточке набора, а требовать текст, которого
        // игрок в бою не увидит, значит заставлять автора писать в стол.
        RequireText(rule.Message, rule.Id, "message");
    }

    private static void RequireText(string value, string ruleId, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Vision rule '{ruleId}' requires {field}.");
        }
    }
}
