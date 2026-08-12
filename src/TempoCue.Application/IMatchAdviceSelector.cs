using TempoCue.Core;

namespace TempoCue.Application;

/// <summary>Выбирает набор и совет для уже нормализованного состояния матча.</summary>
public interface IMatchAdviceSelector
{
    ValueTask<ActiveCoachPackSelection> SelectAsync(
        MatchState match,
        CancellationToken cancellationToken = default);
}
