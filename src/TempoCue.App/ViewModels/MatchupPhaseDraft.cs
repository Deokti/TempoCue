using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>
/// Снимок одной фазы в несохранённом редакторе матчапа.
/// Он не зависит от WPF и позднее преобразуется в проверенную модель Coach Pack.
/// </summary>
public sealed record MatchupPhaseDraft(
    string Id,
    string Name,
    PowerState PowerState,
    int MinimumLevel,
    int? MaximumLevel,
    string PrimaryAdvice,
    string WhyNow,
    string NextGoal,
    IReadOnlyList<MatchupItemDraft> RecommendedItems);
