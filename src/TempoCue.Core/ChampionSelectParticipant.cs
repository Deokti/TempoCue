namespace TempoCue.Core;

/// <summary>
/// Безопасное состояние союзного слота Champion Select. Riot ID существует только
/// для личности, которую League Client явно пометил как видимую.
/// </summary>
public sealed record ChampionSelectParticipant
{
    private ChampionSelectParticipant(
        int cellId,
        int championId,
        PlayerPosition position,
        bool isLocalPlayer,
        ChampionSelectIdentityVisibility identityVisibility,
        RiotId? riotId)
    {
        if (cellId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cellId));
        }

        if (championId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(championId));
        }

        if (identityVisibility == ChampionSelectIdentityVisibility.Visible && riotId is null)
        {
            throw new ArgumentNullException(nameof(riotId));
        }

        if (identityVisibility != ChampionSelectIdentityVisibility.Visible && riotId is not null)
        {
            throw new ArgumentException(
                "Анонимный или недоступный слот не может содержать Riot ID.",
                nameof(riotId));
        }

        CellId = cellId;
        ChampionId = championId;
        Position = position;
        IsLocalPlayer = isLocalPlayer;
        IdentityVisibility = identityVisibility;
        RiotId = riotId;
    }

    public int CellId { get; }

    public int ChampionId { get; }

    public PlayerPosition Position { get; }

    public bool IsLocalPlayer { get; }

    public ChampionSelectIdentityVisibility IdentityVisibility { get; }

    public RiotId? RiotId { get; }

    public static ChampionSelectParticipant Visible(
        int cellId,
        int championId,
        PlayerPosition position,
        bool isLocalPlayer,
        RiotId riotId) => new(
        cellId,
        championId,
        position,
        isLocalPlayer,
        ChampionSelectIdentityVisibility.Visible,
        riotId ?? throw new ArgumentNullException(nameof(riotId)));

    public static ChampionSelectParticipant Anonymous(
        int cellId,
        int championId,
        PlayerPosition position,
        bool isLocalPlayer) => new(
        cellId,
        championId,
        position,
        isLocalPlayer,
        ChampionSelectIdentityVisibility.Anonymous,
        null);

    public static ChampionSelectParticipant Unavailable(
        int cellId,
        int championId,
        PlayerPosition position,
        bool isLocalPlayer) => new(
        cellId,
        championId,
        position,
        isLocalPlayer,
        ChampionSelectIdentityVisibility.Unavailable,
        null);
}
