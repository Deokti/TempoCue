namespace TempoCue.Coaching;

/// <summary>Проверяет постоянную подсказку на границе переносимого набора.</summary>
public static class MapControlPersistentTipValidator
{
    public static void Validate(MapControlPersistentTip tip)
    {
        ArgumentNullException.ThrowIfNull(tip);

        if (string.IsNullOrWhiteSpace(tip.Id)
            || tip.Title.Trim().Length is < 3 or > 80
            || tip.Message.Trim().Length is < 3 or > 240
            || tip.Scope is null)
        {
            throw new InvalidDataException(
                "Map Control persistent tip requires a valid id, title and message.");
        }

        if (!Enum.IsDefined(tip.Scope.Kind))
        {
            throw new InvalidDataException(
                $"Persistent tip '{tip.Id}' has an unsupported scope.");
        }

        if (tip.Scope.Kind == MapControlPersistentTipScopeKind.SpecificObjective)
        {
            if (tip.Scope.Objective is not { } objective || !Enum.IsDefined(objective))
            {
                throw new InvalidDataException(
                    $"Persistent tip '{tip.Id}' requires a supported map objective.");
            }

            return;
        }

        if (tip.Scope.Objective is not null)
        {
            throw new InvalidDataException(
                $"Persistent tip '{tip.Id}' cannot attach an objective to this scope.");
        }
    }
}
