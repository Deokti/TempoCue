namespace TempoCue.App.ViewModels;

/// <summary>Одна безопасная строка союзного состава Champion Select.</summary>
public sealed record ChampionSelectParticipantViewModel(
    string RoleCode,
    string ChampionInitials,
    Uri? ChampionImageUri,
    string ChampionName,
    string Identity,
    string IdentityStatus,
    Uri? ProfileUri,
    string ProfileLinkToolTip,
    bool IsLocalPlayer,
    bool IsAnonymous,
    bool IsChampionPending)
{
    public bool HasProfileUri => ProfileUri is not null;
}
