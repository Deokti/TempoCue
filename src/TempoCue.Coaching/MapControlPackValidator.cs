namespace TempoCue.Coaching;

/// <summary>Проверяет переносимый набор контроля карты на границе системы.</summary>
public static class MapControlPackValidator
{
    public const int CurrentSchemaVersion = 1;

    public static void Validate(MapControlPack pack)
    {
        ArgumentNullException.ThrowIfNull(pack);

        if (pack.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported Map Control Pack schema: {pack.SchemaVersion}.");
        }

        if (!string.Equals(
                pack.PackType,
                MapControlPack.SupportedPackType,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Unsupported Coach Pack type: {pack.PackType}.");
        }

        if (string.IsNullOrWhiteSpace(pack.Id)
            || string.IsNullOrWhiteSpace(pack.Name)
            || string.IsNullOrWhiteSpace(pack.Patch))
        {
            throw new InvalidDataException("Map Control Pack id, name and patch are required.");
        }

        if (string.IsNullOrWhiteSpace(pack.Description)
            || string.IsNullOrWhiteSpace(pack.Author)
            || string.IsNullOrWhiteSpace(pack.ContentLanguage)
            || pack.VerifiedOn is null
            || pack.Source is null)
        {
            throw new InvalidDataException("Map Control Pack metadata is incomplete.");
        }

        if (string.Equals(pack.Id, pack.ForkedFromPackId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("A Map Control Pack copy must have its own id.");
        }

        if (pack.ForkedFromAuthor is not null && string.IsNullOrWhiteSpace(pack.ForkedFromPackId))
        {
            throw new InvalidDataException("Fork author requires the source Map Control Pack id.");
        }

        ValidateApplicability(pack.Applicability);

        if (pack.Rules.Count == 0
            && pack.TimedReminders.Count == 0
            && pack.PersistentTips.Count == 0)
        {
            throw new InvalidDataException(
                "Map Control Pack must contain at least one objective rule, timed reminder or persistent tip.");
        }

        if (!MapObjectiveTimingCatalog.TryGetForPatch(pack.Patch, out var timingSet)
            || timingSet is null)
        {
            throw new InvalidDataException(
                $"Map objective timings are unavailable for patch '{pack.Patch}'.");
        }

        CoachPackValidation.ValidateSource(pack.Source);
        CoachPackValidation.EnsureUniqueIds(
            pack.Rules.Select(static rule => rule.Id),
            "map reminder rule");
        CoachPackValidation.EnsureUniqueIds(
            pack.TimedReminders.Select(static reminder => reminder.Id),
            "map timed reminder");
        CoachPackValidation.EnsureUniqueIds(
            pack.PersistentTips.Select(static tip => tip.Id),
            "map persistent tip");

        var timings = timingSet.Objectives.ToDictionary(static timing => timing.Objective);
        foreach (var rule in pack.Rules)
        {
            if (!Enum.IsDefined(rule.Objective)
                || !timings.TryGetValue(rule.Objective, out var timing))
            {
                throw new InvalidDataException(
                    $"Rule '{rule.Id}' references an unsupported map objective.");
            }

            MapObjectiveReminderRuleValidator.Validate(rule, timing);
        }

        foreach (var tip in pack.PersistentTips)
        {
            MapControlPersistentTipValidator.Validate(tip);
        }

        foreach (var reminder in pack.TimedReminders)
        {
            MapControlTimedReminderValidator.Validate(reminder);
        }
    }

    private static void ValidateApplicability(MapControlApplicability? applicability)
    {
        if (applicability is null
            || applicability.Roles is null
            || applicability.Roles.Count == 0)
        {
            throw new InvalidDataException("Map Control Pack must apply to at least one role.");
        }

        if (!Enum.IsDefined(applicability.TeamSide)
            || applicability.Roles.Any(static role => !Enum.IsDefined(role))
            || applicability.Roles.Distinct().Count() != applicability.Roles.Count)
        {
            throw new InvalidDataException("Map Control Pack applicability is invalid.");
        }
    }
}
