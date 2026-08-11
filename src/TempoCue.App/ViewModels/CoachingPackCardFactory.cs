using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Преобразует доменные данные Coach Pack в локализованное представление карточки.</summary>
internal sealed class CoachingPackCardFactory
{
    private readonly LocalizationService _localization;

    public CoachingPackCardFactory(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public CoachingPackCardViewModel Create(
        CoachPack pack,
        CoachingPackOrigin origin,
        LocalCoachPackState state,
        IReadOnlyDictionary<string, ChampionCatalogEntry> champions)
    {
        ArgumentNullException.ThrowIfNull(pack);
        ArgumentNullException.ThrowIfNull(champions);

        var firstRule = pack.Rules.FirstOrDefault();
        var playerChampionId = EmptyToFallback(pack.PlayerChampion, firstRule?.PlayerChampion);
        var opponentChampionId = EmptyToFallback(pack.OpponentChampion, firstRule?.OpponentChampion);
        champions.TryGetValue(playerChampionId, out var playerChampion);
        champions.TryGetValue(opponentChampionId, out var opponentChampion);
        var playerName = playerChampion?.DisplayName ?? playerChampionId;
        var opponentName = opponentChampion?.DisplayName ?? opponentChampionId;
        var author = string.IsNullOrWhiteSpace(pack.Author)
            ? _localization.Get("Coaching.Library.Author.TempoCue")
            : pack.Author;
        var role = string.IsNullOrWhiteSpace(pack.LaneRole)
            ? _localization.Get("Coaching.Library.Role.Any")
            : pack.LaneRole.ToUpperInvariant();
        var adviceCount = pack.Rules.Count + pack.PersistentTips.Count;

        return new CoachingPackCardViewModel(
            pack,
            origin,
            state,
            _localization.Format("Coaching.Library.Matchup", playerName, opponentName),
            playerName,
            opponentName,
            GetInitials(playerName),
            GetInitials(opponentName),
            playerChampion?.ImageUri,
            opponentChampion?.ImageUri,
            _localization.Format("Coaching.Library.Card.Details", role, author),
            _localization.Plural("Count.Advice", adviceCount),
            _localization.Get(GetStateResourceKey(state)),
            _localization.Get(origin switch
            {
                CoachingPackOrigin.BuiltIn => "Coaching.Library.Origin.BuiltIn",
                CoachingPackOrigin.Imported => "Coaching.Library.Origin.Imported",
                _ => "Coaching.Library.Origin.Created"
            }),
            GetContentLanguageLabel(pack.ContentLanguage),
            _localization.Format("Coaching.Library.Patch", pack.Patch));
    }

    private string GetContentLanguageLabel(string contentLanguage)
    {
        if (contentLanguage.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
        {
            return _localization.Get("Coaching.Library.Language.Russian");
        }

        if (contentLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return _localization.Get("Coaching.Library.Language.English");
        }

        return string.IsNullOrWhiteSpace(contentLanguage)
            ? _localization.Get("Coaching.Library.Language.Unknown")
            : contentLanguage;
    }

    private static string GetStateResourceKey(LocalCoachPackState state) => state switch
    {
        LocalCoachPackState.Enabled => "Coaching.Library.State.Enabled",
        LocalCoachPackState.Disabled => "Coaching.Library.State.Disabled",
        _ => "Coaching.Library.State.Draft"
    };

    private static string EmptyToFallback(string value, string? fallback) =>
        string.IsNullOrWhiteSpace(value)
            ? fallback ?? "—"
            : value;

    private static string GetInitials(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Take(3)
            .Select(char.ToUpperInvariant)
            .ToArray());
    }
}
