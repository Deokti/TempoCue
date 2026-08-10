using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Собирает проверяемую Core-модель из состояния редактора матчапа.</summary>
internal sealed class MatchupCoachPackFactory
{
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    public CoachPack Create(
        MatchupMainInformationViewModel mainInformation,
        MatchupChampionsAndRoleViewModel champions,
        MatchupContentViewModel content,
        MatchupSourceDraft sourceDraft)
    {
        ArgumentNullException.ThrowIfNull(mainInformation);
        ArgumentNullException.ThrowIfNull(champions);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(sourceDraft);

        var playerChampion = champions.SelectedPlayerChampion
            ?? throw new InvalidOperationException("Чемпион игрока не выбран.");
        var opponentChampion = champions.SelectedOpponentChampion
            ?? throw new InvalidOperationException("Чемпион противника не выбран.");
        var source = CreateSource(sourceDraft);
        var sourceSummary = CreateRuleSource(source, mainInformation.AdviceLanguageCode);

        return new CoachPack
        {
            SchemaVersion = 2,
            Id = $"matchup.{NormalizeId(playerChampion.Id)}-vs-{NormalizeId(opponentChampion.Id)}.{_instanceId}",
            Name = mainInformation.PackName.Trim(),
            Patch = mainInformation.GamePatch,
            Description = mainInformation.ShortDescription.Trim(),
            Author = mainInformation.Author.Trim(),
            ContentLanguage = mainInformation.AdviceLanguageCode,
            PlayerChampion = playerChampion.Id,
            OpponentChampion = opponentChampion.Id,
            LaneRole = champions.SelectedRole.ToString().ToLowerInvariant(),
            VerifiedOn = DateOnly.FromDateTime(mainInformation.VerifiedOn),
            Source = source,
            Rules = content.Phases.Select((phase, index) => new CoachRuleDefinition
            {
                Id = phase.Id,
                PlayerChampion = playerChampion.Id,
                OpponentChampion = opponentChampion.Id,
                MinimumLevel = phase.MinimumLevel,
                MaximumLevel = phase.MaximumLevel,
                PowerState = phase.PowerState,
                Priority = 1000 - index,
                Title = phase.Name,
                Message = phase.PrimaryAdvice,
                WhyNow = phase.WhyNow,
                NextGoal = phase.NextGoal,
                RecommendedItemIds = phase.RecommendedItems.Select(static item => item.Id).ToArray(),
                Source = sourceSummary
            }).ToArray(),
            PersistentTips = content.PersistentTips.Tips.Select(tip => new CoachPersistentTipDefinition
            {
                Id = tip.Id,
                Title = tip.Title,
                Message = tip.Text,
                ApplicableRuleId = tip.ApplicablePhaseId,
                Source = tip.CustomSource
            }).ToArray(),
            ItemBlocks = content.ItemBlocks.Blocks.Select(block => new CoachItemBlockDefinition
            {
                Id = block.Id,
                Name = block.Name,
                WhenToChoose = block.WhenToChoose,
                ItemIds = block.Items.Select(static item => item.Id).ToArray()
            }).ToArray()
        };
    }

    private static CoachPackSource CreateSource(MatchupSourceDraft source) => new()
    {
        Kind = source.Kind switch
        {
            MatchupSourceKind.PersonalExperience => CoachPackSourceKind.PersonalExperience,
            MatchupSourceKind.Article => CoachPackSourceKind.Article,
            MatchupSourceKind.Video => CoachPackSourceKind.Video,
            MatchupSourceKind.Guide => CoachPackSourceKind.CommunityGuide,
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        },
        Title = source.Title.Trim(),
        Url = source.Url.Trim(),
        Author = source.Author.Trim(),
        Permission = source.Kind == MatchupSourceKind.PersonalExperience
            ? CoachPackSourcePermission.NotRequired
            : source.AuthorPermissionGranted
                ? CoachPackSourcePermission.Granted
                : CoachPackSourcePermission.NotConfirmed
    };

    private static string CreateRuleSource(CoachPackSource source, string languageCode)
    {
        if (source.Kind == CoachPackSourceKind.PersonalExperience)
        {
            return languageCode.StartsWith("ru", StringComparison.OrdinalIgnoreCase)
                ? "Личный опыт автора"
                : "Author's personal experience";
        }

        return string.IsNullOrWhiteSpace(source.Author)
            ? source.Title
            : $"{source.Title} — {source.Author}";
    }

    private static string NormalizeId(string value) => new(
        value
            .Where(static character => char.IsLetterOrDigit(character))
            .Select(static character => char.ToLowerInvariant(character))
            .ToArray());
}

internal sealed record MatchupSourceDraft(
    MatchupSourceKind Kind,
    string Title,
    string Url,
    string Author,
    bool AuthorPermissionGranted);
