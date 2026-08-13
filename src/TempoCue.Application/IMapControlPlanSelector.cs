namespace TempoCue.Application;

/// <summary>Выбирает применимые наборы контроля карты и строит их общий план матча.</summary>
public interface IMapControlPlanSelector
{
    ValueTask<ActiveMapControlPlan> SelectAsync(
        MatchSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
