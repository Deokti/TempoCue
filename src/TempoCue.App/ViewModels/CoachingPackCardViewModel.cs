using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Готовое для отображения представление одного матчапа в библиотеке.</summary>
public sealed record CoachingPackCardViewModel(
    CoachPack Pack,
    CoachingPackOrigin Origin,
    LocalCoachPackState State,
    string Title,
    string PlayerChampionName,
    string OpponentChampionName,
    string PlayerChampionInitials,
    string OpponentChampionInitials,
    Uri? PlayerChampionImageUri,
    Uri? OpponentChampionImageUri,
    string Details,
    string AdviceCount,
    string StateLabel,
    string OriginLabel,
    string ContentLanguageLabel,
    string PatchLabel,
    string DeleteActionLabel)
{
    public bool IsEnabled => State == LocalCoachPackState.Enabled;

    public bool IsDraft => State == LocalCoachPackState.Draft;

    public bool IsDisabled => State == LocalCoachPackState.Disabled;

    public bool IsCreatedByUser => Origin == CoachingPackOrigin.CreatedByUser;

    public bool CanEdit => IsCreatedByUser;

    public bool CanCreateCopy => !IsCreatedByUser;

    public bool CanDelete => Origin != CoachingPackOrigin.BuiltIn;
}
