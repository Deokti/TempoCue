namespace TempoCue.Application;

/// <summary>Результат успешной записи Coach Pack во внешнее хранилище.</summary>
public sealed record CoachPackSaveResult(
    string PackId,
    LocalCoachPackState State,
    string DisplayPath);
