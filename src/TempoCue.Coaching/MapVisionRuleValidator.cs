namespace TempoCue.Coaching;

/// <summary>Проверяет правило уведомления по наблюдению на миникарте.</summary>
internal static class MapVisionRuleValidator
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

        // Текст обязателен: правило без сообщения показало бы игроку пустое
        // уведомление, а «почему сейчас» и «что дальше» — то, ради чего наборы
        // и заведены вместо простых галочек.
        RequireText(rule.Message, rule.Id, "message");
        RequireText(rule.WhyNow, rule.Id, "whyNow");
        RequireText(rule.NextGoal, rule.Id, "nextGoal");
    }

    private static void RequireText(string value, string ruleId, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Vision rule '{ruleId}' requires {field}.");
        }
    }
}
