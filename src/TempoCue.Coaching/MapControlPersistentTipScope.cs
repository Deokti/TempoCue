using TempoCue.Core;

namespace TempoCue.Coaching;

/// <summary>Область применимости постоянной подсказки контроля карты.</summary>
public sealed record MapControlPersistentTipScope(
    MapControlPersistentTipScopeKind Kind,
    MapObjectiveKind? Objective = null);
