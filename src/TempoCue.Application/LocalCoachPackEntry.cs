using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Проверенный пользовательский Coach Pack вместе с его локальным состоянием.</summary>
public sealed record LocalCoachPackEntry(
    CoachPack Pack,
    LocalCoachPackState State,
    DateTimeOffset SavedAtUtc,
    LocalCoachPackOrigin Origin = LocalCoachPackOrigin.CreatedByUser);
