using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Состояние активной сессии, готовое для представления.</summary>
public sealed record ActiveMatchUpdate(
    MatchSnapshotReadStatus Status,
    MatchSnapshot? Snapshot,
    ActiveCoachPackSelection? CoachPackSelection,
    ActiveMapControlPlan? MapControlPlan = null)
{
    public CoachAdvice? Advice => CoachPackSelection?.Advice;
}
