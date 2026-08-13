namespace TempoCue.Coaching;

/// <summary>Готовая к показу постоянная подсказка одного активного набора контроля карты.</summary>
public sealed record MapControlPersistentAdvice(
    string Id,
    string PackId,
    string PackName,
    string Title,
    string Message);
