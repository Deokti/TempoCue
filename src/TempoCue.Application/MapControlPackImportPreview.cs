namespace TempoCue.Application;

/// <summary>Результат безопасной проверки переносимого набора контроля карты.</summary>
public sealed record MapControlPackImportPreview(
    MapControlPack Pack,
    MapControlPackImportConflict Conflict,
    LocalCoachPackState? ExistingState)
{
    public bool CanImport => Conflict is MapControlPackImportConflict.None
        or MapControlPackImportConflict.ImportedPack;

    public bool RequiresReplacement => Conflict == MapControlPackImportConflict.ImportedPack;
}

public enum MapControlPackImportConflict
{
    None,
    ImportedPack,
    CreatedPack,
    BuiltInPack
}

public sealed record MapControlPackImportResult(
    bool Succeeded,
    MapControlPackImportPreview Preview,
    MapControlPackSaveResult? SaveResult);
