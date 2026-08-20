namespace TempoCue.App.ViewModels;

/// <summary>Одна строка состава команды.</summary>
public sealed record ParticipantViewModel(
    string RoleCode,
    string ChampionInitials,
    Uri? ChampionImageUri,
    string ChampionName,
    string RiotId,
    Uri? ProfileUri,
    string ProfileLinkToolTip,
    string Details,
    string Kda,
    int Level,
    bool IsActivePlayer,
    bool IsDead)
{
    public bool HasRiotId => !string.IsNullOrWhiteSpace(RiotId);

    public bool HasProfileUri => ProfileUri is not null;
}
