using TempoCue.Application;
using TempoCue.Core;

namespace TempoCue.App.Tests;

/// <summary>Возвращает совет из фиксированного набора правил без локального хранилища.</summary>
internal sealed class TestMatchAdviceSelector : IMatchAdviceSelector
{
    private readonly Func<MatchState, ActiveCoachPackSelection> _select;

    public TestMatchAdviceSelector(IEnumerable<CoachRuleDefinition>? rules = null)
    {
        var pack = new CoachPack
        {
            SchemaVersion = 2,
            Id = "test.active-match",
            Name = "Тестовый набор",
            Patch = "test",
            Rules = rules?.ToArray() ?? []
        };
        _select = match => pack.Rules.Count == 0
            ? ActiveCoachPackSelection.NotFound()
            : ActiveCoachPackSelection.Selected(
                pack,
                new MatchupCoach(pack.Rules).GetBestAdvice(match));
    }

    public TestMatchAdviceSelector(ActiveCoachPackSelection selection)
    {
        ArgumentNullException.ThrowIfNull(selection);
        _select = _ => selection;
    }

    public ValueTask<ActiveCoachPackSelection> SelectAsync(
        MatchState match,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(_select(match));
    }
}
