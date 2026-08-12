using TempoCue.App.Localization;
using TempoCue.Application;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Преобразует Map Control Pack в локализованную карточку библиотеки.</summary>
internal sealed class MapControlPackCardFactory
{
    private readonly LocalizationService _localization;

    public MapControlPackCardFactory(LocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public MapControlPackCardViewModel Create(
        MapControlPack pack,
        CoachingPackOrigin origin,
        LocalCoachPackState state)
    {
        ArgumentNullException.ThrowIfNull(pack);
        var author = origin == CoachingPackOrigin.CreatedByUser
            ? _localization.Get("Coaching.MapControl.Library.Author.You")
            : string.IsNullOrWhiteSpace(pack.Author)
                ? _localization.Get("Coaching.Library.Author.TempoCue")
                : pack.Author;
        var adviceCount = pack.Rules.Count
                          + pack.TimedReminders.Count
                          + pack.PersistentTips.Count;

        return new MapControlPackCardViewModel(
            pack,
            origin,
            state,
            pack.Name,
            _localization.Format(
                "Coaching.MapControl.Library.Card.Details",
                GetRolesLabel(pack.Applicability.Roles),
                author),
            _localization.Plural("Count.Advice", adviceCount),
            _localization.Get(GetStateResourceKey(state)),
            _localization.Get(origin == CoachingPackOrigin.Imported
                ? "Coaching.Library.Origin.Imported"
                : "Coaching.Library.Origin.Created"),
            GetContentLanguageLabel(pack.ContentLanguage),
            _localization.Format("Coaching.Library.Patch", pack.Patch));
    }

    private string GetRolesLabel(IReadOnlyList<MapControlRole> roles)
    {
        if (roles.Count == MapControlRoleValues.All.Count
            && MapControlRoleValues.All.All(roles.Contains))
        {
            return _localization.Get("Coaching.MapControlEditor.Scope.Summary.AllRoles");
        }

        return _localization.Format(
            "Coaching.MapControl.Library.Role.Selected",
            string.Join(", ", roles.Select(ToDisplayRole)));
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

    private static string ToDisplayRole(MapControlRole role) => role switch
    {
        MapControlRole.Top => "TOP",
        MapControlRole.Jungle => "JUNGLE",
        MapControlRole.Mid => "MID",
        MapControlRole.Adc => "ADC",
        MapControlRole.Support => "SUPPORT",
        _ => role.ToString().ToUpperInvariant()
    };

    private static string GetStateResourceKey(LocalCoachPackState state) => state switch
    {
        LocalCoachPackState.Enabled => "Coaching.Library.State.Enabled",
        LocalCoachPackState.Disabled => "Coaching.Library.State.Disabled",
        _ => "Coaching.Library.State.Draft"
    };
}
