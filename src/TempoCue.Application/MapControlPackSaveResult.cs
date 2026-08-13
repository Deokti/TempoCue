namespace TempoCue.Application;

/// <summary>Результат успешной записи Map Control Pack.</summary>
public sealed record MapControlPackSaveResult(
    string PackId,
    LocalCoachPackState State,
    string DisplayPath);
