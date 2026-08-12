using TempoCue.Application;
using TempoCue.Coaching;

namespace TempoCue.App.ViewModels;

/// <summary>Готовое для отображения представление набора контроля карты в библиотеке.</summary>
public sealed record MapControlPackCardViewModel(
    MapControlPack Pack,
    CoachingPackOrigin Origin,
    LocalCoachPackState State,
    string Title,
    string Details,
    string AdviceCount,
    string StateLabel,
    string OriginLabel,
    string ContentLanguageLabel,
    string PatchLabel)
{
    public bool IsEnabled => State == LocalCoachPackState.Enabled;

    public bool IsDraft => State == LocalCoachPackState.Draft;

    public bool IsDisabled => State == LocalCoachPackState.Disabled;

    public bool IsCreatedByUser => Origin == CoachingPackOrigin.CreatedByUser;
}
