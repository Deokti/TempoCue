using TempoCue.Core;

namespace TempoCue.App.ViewModels;

/// <summary>Одно правило Coach Pack в формате, готовом для отображения.</summary>
public sealed record CoachingRuleViewModel(
    PowerState PowerState,
    string PowerStateLabel,
    string LevelRange,
    string Title,
    string Message,
    string Source);
