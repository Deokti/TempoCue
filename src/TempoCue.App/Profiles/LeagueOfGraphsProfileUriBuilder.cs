using TempoCue.Core;

namespace TempoCue.App.Profiles;

/// <summary>Строит ссылку на профиль только для поддерживаемого региона LeagueOfGraphs.</summary>
public static class LeagueOfGraphsProfileUriBuilder
{
    private static readonly IReadOnlyDictionary<string, string> RegionSlugs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BR"] = "br",
            ["BR1"] = "br",
            ["EUNE"] = "eune",
            ["EUN1"] = "eune",
            ["EUW"] = "euw",
            ["EUW1"] = "euw",
            ["JP"] = "jp",
            ["JP1"] = "jp",
            ["KR"] = "kr",
            ["LAN"] = "lan",
            ["LA1"] = "lan",
            ["LAS"] = "las",
            ["LA2"] = "las",
            ["NA"] = "na",
            ["NA1"] = "na",
            ["OCE"] = "oce",
            ["OC1"] = "oce",
            ["RU"] = "ru",
            ["TR"] = "tr",
            ["TR1"] = "tr",
            ["PH"] = "ph",
            ["PH2"] = "ph",
            ["SG"] = "sg",
            ["SG2"] = "sg",
            ["TH"] = "th",
            ["TH2"] = "th",
            ["TW"] = "tw",
            ["TW2"] = "tw",
            ["VN"] = "vn",
            ["VN2"] = "vn",
            ["ME"] = "me",
            ["ME1"] = "me"
        };

    public static Uri? Build(RiotId? riotId, string? clientRegion)
    {
        if (riotId is null
            || string.IsNullOrWhiteSpace(clientRegion)
            || !RegionSlugs.TryGetValue(clientRegion.Trim(), out var regionSlug))
        {
            return null;
        }

        var gameName = EscapePathPart(riotId.GameName);
        var tagLine = EscapePathPart(riotId.TagLine);
        return new Uri(
            $"https://www.leagueofgraphs.com/summoner/{regionSlug}/{gameName}-{tagLine}",
            UriKind.Absolute);
    }

    private static string EscapePathPart(string value) =>
        Uri.EscapeDataString(value).Replace("%20", "+", StringComparison.Ordinal);
}
