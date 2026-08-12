using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Результат безопасной проверки переносимого Coach Pack перед записью.</summary>
public sealed record CoachPackImportPreview(
    CoachPack Pack,
    CoachPackImportConflict Conflict,
    LocalCoachPackState? ExistingState)
{
    public bool CanImport => Conflict is CoachPackImportConflict.None
        or CoachPackImportConflict.ImportedPack;

    public bool RequiresReplacement => Conflict == CoachPackImportConflict.ImportedPack;
}

/// <summary>Локальный конфликт идентификатора импортируемого Coach Pack.</summary>
public enum CoachPackImportConflict
{
    None,
    ImportedPack,
    CreatedPack,
    BuiltInPack
}

/// <summary>Результат записи или повторной проверки непосредственно перед ней.</summary>
public sealed record CoachPackImportResult(
    bool Succeeded,
    CoachPackImportPreview Preview,
    CoachPackSaveResult? SaveResult);
