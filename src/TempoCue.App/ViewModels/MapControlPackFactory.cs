using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Собирает переносимый Map Control Pack из проверенного состояния редактора.</summary>
internal sealed class MapControlPackFactory
{
    public MapControlPack Create(
        MapControlMainInformationViewModel mainInformation,
        MapControlScopeViewModel scope,
        MapControlContentViewModel content,
        MapControlSourceDraft sourceDraft,
        string? packId = null,
        string? forkedFromPackId = null,
        string? forkedFromAuthor = null)
    {
        ArgumentNullException.ThrowIfNull(mainInformation);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourceDraft);

        return new MapControlPack
        {
            SchemaVersion = MapControlPackValidator.CurrentSchemaVersion,
            PackType = MapControlPack.SupportedPackType,
            Id = packId ?? $"map-control.{Guid.NewGuid():N}",
            Name = mainInformation.PackName.Trim(),
            Patch = mainInformation.GamePatch,
            Description = mainInformation.ShortDescription.Trim(),
            Author = CreatePackAuthor(mainInformation.ContentLanguageCode, sourceDraft),
            ContentLanguage = mainInformation.ContentLanguageCode,
            VerifiedOn = DateOnly.FromDateTime(DateTime.Today),
            Source = CreateSource(sourceDraft),
            ForkedFromPackId = forkedFromPackId,
            ForkedFromAuthor = forkedFromAuthor,
            Applicability = new MapControlApplicability
            {
                Roles = scope.SelectedRoles.ToArray(),
                TeamSide = scope.SelectedTeamSide
            },
            Rules = content.Rules.Select(static rule => rule.Rule).ToArray(),
            TimedReminders = content.TimedReminders.Snapshot,
            PersistentTips = content.PersistentTips.Snapshot,
            VisionRules = content.VisionRules.Snapshot
        };
    }

    private static string CreatePackAuthor(
        string languageCode,
        MapControlSourceDraft sourceDraft)
    {
        if (!string.IsNullOrWhiteSpace(sourceDraft.Author))
        {
            return sourceDraft.Author.Trim();
        }

        return languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
            ? "Пользователь TempoCue"
            : "TempoCue user";
    }

    private static CoachPackSource CreateSource(MapControlSourceDraft sourceDraft) => new()
    {
        Kind = sourceDraft.Kind,
        Title = sourceDraft.Title.Trim(),
        Url = sourceDraft.Url.Trim(),
        Author = sourceDraft.Author.Trim(),
        Permission = sourceDraft.Kind == CoachPackSourceKind.PersonalExperience
            ? CoachPackSourcePermission.NotRequired
            : CoachPackSourcePermission.NotConfirmed
    };
}

internal sealed record MapControlSourceDraft(
    CoachPackSourceKind Kind,
    string Title,
    string Url,
    string Author);
