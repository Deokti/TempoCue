namespace TempoCue.Coaching;

/// <summary>
/// Возвращает проверенные тайминги эпических объектов для выбранного патча.
/// Для ещё не проверенного будущего патча каталог сохраняет работоспособность на последних
/// известных данных, но явно помечает результат как требующий повторной проверки.
/// </summary>
public static class MapObjectiveTimingCatalog
{
    private static readonly PatchVersion FirstSupportedPatch = new(15, 9);
    private static readonly PatchVersion BaronTwentyMinutePatch = new(16, 1);
    private static readonly PatchVersion LastVerifiedPatch = new(16, 15);

    private static readonly IReadOnlyList<MapObjectiveTiming> Patch159Timings =
    [
        Create(MapObjectiveKind.ElementalDragon, minutes: 5, respawnMinutes: 5),
        Create(MapObjectiveKind.VoidGrubs, minutes: 8),
        Create(MapObjectiveKind.RiftHerald, minutes: 15),
        Create(MapObjectiveKind.BaronNashor, minutes: 25, respawnMinutes: 6)
    ];

    private static readonly IReadOnlyList<MapObjectiveTiming> Patch161Timings =
    [
        Create(MapObjectiveKind.ElementalDragon, minutes: 5, respawnMinutes: 5),
        Create(MapObjectiveKind.VoidGrubs, minutes: 8),
        Create(MapObjectiveKind.RiftHerald, minutes: 15),
        Create(MapObjectiveKind.BaronNashor, minutes: 20, respawnMinutes: 6)
    ];

    /// <summary>
    /// Пытается найти график для патча Data Dragon (например, 16.15) или публичного
    /// номера сезона Riot (например, 26.15).
    /// </summary>
    public static bool TryGetForPatch(string patch, out MapObjectiveTimingSet? timingSet)
    {
        timingSet = null;
        if (!PatchVersion.TryParse(patch, out var requestedPatch)
            || requestedPatch < FirstSupportedPatch)
        {
            return false;
        }

        var schedulePatch = requestedPatch > LastVerifiedPatch
            ? LastVerifiedPatch
            : requestedPatch;
        var effectivePatch = schedulePatch >= BaronTwentyMinutePatch
            ? BaronTwentyMinutePatch
            : FirstSupportedPatch;
        var timings = schedulePatch >= BaronTwentyMinutePatch
            ? Patch161Timings
            : Patch159Timings;

        timingSet = new MapObjectiveTimingSet(
            requestedPatch.ToString(),
            effectivePatch.ToString(),
            LastVerifiedPatch.ToString(),
            requestedPatch <= LastVerifiedPatch,
            timings);
        return true;
    }

    private static MapObjectiveTiming Create(
        MapObjectiveKind objective,
        int minutes,
        int? respawnMinutes = null) =>
        new(
            objective,
            TimeSpan.FromMinutes(minutes),
            respawnMinutes is null ? null : TimeSpan.FromMinutes(respawnMinutes.Value));

    private readonly record struct PatchVersion(int Major, int Minor) : IComparable<PatchVersion>
    {
        public int CompareTo(PatchVersion other)
        {
            var majorComparison = Major.CompareTo(other.Major);
            return majorComparison != 0
                ? majorComparison
                : Minor.CompareTo(other.Minor);
        }

        public override string ToString() => $"{Major}.{Minor}";

        public static bool TryParse(string? value, out PatchVersion patch)
        {
            patch = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2
                || !int.TryParse(parts[0], out var major)
                || !int.TryParse(parts[1], out var minor)
                || major < 0
                || minor < 0)
            {
                return false;
            }

            // В 2025 Riot перешла на публичные номера 25.x, тогда как Data Dragon
            // продолжил отдавать техническую ветку 15.x. Coach Pack хранит версию Data Dragon.
            if (major >= 25)
            {
                major -= 10;
            }

            patch = new PatchVersion(major, minor);
            return true;
        }

        public static bool operator <(PatchVersion left, PatchVersion right) =>
            left.CompareTo(right) < 0;

        public static bool operator >(PatchVersion left, PatchVersion right) =>
            left.CompareTo(right) > 0;

        public static bool operator >=(PatchVersion left, PatchVersion right) =>
            left.CompareTo(right) >= 0;

        public static bool operator <=(PatchVersion left, PatchVersion right) =>
            left.CompareTo(right) <= 0;
    }
}
