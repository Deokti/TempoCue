namespace TempoCue.Coaching;

/// <summary>Определяет контекст, в котором игроку полезна постоянная подсказка контроля карты.</summary>
public enum MapControlPersistentTipScopeKind
{
    WholeMatch,
    BeforeFirstObjective,
    WhileAnyObjectiveAvailable,
    SpecificObjective
}
