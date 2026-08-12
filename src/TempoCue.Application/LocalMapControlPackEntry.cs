namespace TempoCue.Application;

/// <summary>Проверенный Map Control Pack вместе с его локальным состоянием.</summary>
public sealed record LocalMapControlPackEntry(
    MapControlPack Pack,
    LocalCoachPackState State,
    DateTimeOffset SavedAtUtc,
    LocalCoachPackOrigin Origin = LocalCoachPackOrigin.CreatedByUser);
