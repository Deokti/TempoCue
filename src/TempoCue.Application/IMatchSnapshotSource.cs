namespace TempoCue.Application;

/// <summary>Внешний источник состояния текущего матча.</summary>
public interface IMatchSnapshotSource
{
    Task<MatchSnapshotReadResult> ReadAsync(CancellationToken cancellationToken = default);
}
