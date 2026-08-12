using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Проверенный переносимый файл одного из поддерживаемых видов Coach Pack.</summary>
public sealed record PortableCoachPackImportPreview
{
    private PortableCoachPackImportPreview(
        CoachPackImportPreview? matchupPreview,
        MapControlPackImportPreview? mapControlPreview,
        PortableCoachPackImportConflict conflict)
    {
        MatchupPreview = matchupPreview;
        MapControlPreview = mapControlPreview;
        Conflict = conflict;
    }

    public CoachPackImportPreview? MatchupPreview { get; }

    public MapControlPackImportPreview? MapControlPreview { get; }

    public CoachPack? MatchupPack => MatchupPreview?.Pack;

    public MapControlPack? MapControlPack => MapControlPreview?.Pack;

    public PortableCoachPackImportConflict Conflict { get; }

    public bool CanImport => Conflict is PortableCoachPackImportConflict.None
        or PortableCoachPackImportConflict.ImportedPack;

    public bool RequiresReplacement => Conflict == PortableCoachPackImportConflict.ImportedPack;

    public static PortableCoachPackImportPreview FromMatchup(
        CoachPackImportPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return new PortableCoachPackImportPreview(
            preview,
            null,
            preview.Conflict switch
            {
                CoachPackImportConflict.None => PortableCoachPackImportConflict.None,
                CoachPackImportConflict.ImportedPack =>
                    PortableCoachPackImportConflict.ImportedPack,
                CoachPackImportConflict.CreatedPack =>
                    PortableCoachPackImportConflict.CreatedPack,
                CoachPackImportConflict.BuiltInPack =>
                    PortableCoachPackImportConflict.BuiltInPack,
                _ => throw new ArgumentOutOfRangeException(nameof(preview))
            });
    }

    public static PortableCoachPackImportPreview FromMapControl(
        MapControlPackImportPreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        return new PortableCoachPackImportPreview(
            null,
            preview,
            preview.Conflict switch
            {
                MapControlPackImportConflict.None => PortableCoachPackImportConflict.None,
                MapControlPackImportConflict.ImportedPack =>
                    PortableCoachPackImportConflict.ImportedPack,
                MapControlPackImportConflict.CreatedPack =>
                    PortableCoachPackImportConflict.CreatedPack,
                MapControlPackImportConflict.BuiltInPack =>
                    PortableCoachPackImportConflict.BuiltInPack,
                _ => throw new ArgumentOutOfRangeException(nameof(preview))
            });
    }
}

public enum PortableCoachPackImportConflict
{
    None,
    ImportedPack,
    CreatedPack,
    BuiltInPack
}

public sealed record PortableCoachPackImportResult(
    bool Succeeded,
    PortableCoachPackImportPreview Preview);
