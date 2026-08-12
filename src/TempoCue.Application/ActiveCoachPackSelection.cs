using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Выбранный набор и вычисленный из него совет для одного снимка матча.</summary>
public sealed record ActiveCoachPackSelection(
    ActiveCoachPackSelectionState State,
    CoachPack? Pack,
    CoachAdvice? Advice,
    IReadOnlyList<string> ConflictingPackNames)
{
    public static ActiveCoachPackSelection MatchupUnavailable() => new(
        ActiveCoachPackSelectionState.MatchupUnavailable,
        null,
        null,
        []);

    public static ActiveCoachPackSelection NotFound() => new(
        ActiveCoachPackSelectionState.NotFound,
        null,
        null,
        []);

    public static ActiveCoachPackSelection Selected(
        CoachPack pack,
        CoachAdvice? advice) => new(
            ActiveCoachPackSelectionState.Selected,
            pack,
            advice,
            []);

    public static ActiveCoachPackSelection Conflict(
        IReadOnlyList<string> conflictingPackNames) => new(
            ActiveCoachPackSelectionState.Conflict,
            null,
            null,
            conflictingPackNames);

    public static ActiveCoachPackSelection LibraryUnavailable() => new(
        ActiveCoachPackSelectionState.LibraryUnavailable,
        null,
        null,
        []);
}
