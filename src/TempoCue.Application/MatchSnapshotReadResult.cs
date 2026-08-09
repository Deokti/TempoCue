namespace TempoCue.Application;

/// <summary>Возможные исходы чтения очередного снимка матча.</summary>
public enum MatchSnapshotReadStatus
{
    Success,
    NoActiveMatch,
    GameLoading,
    Timeout,
    InvalidData,
    Failed
}

/// <summary>
/// Типизированный результат внешнего источника. Ожидаемое отсутствие матча
/// не используется как исключение в сценарии приложения.
/// </summary>
public sealed record MatchSnapshotReadResult
{
    private MatchSnapshotReadResult(
        MatchSnapshotReadStatus status,
        MatchSnapshot? snapshot = null)
    {
        Status = status;
        Snapshot = snapshot;
    }

    public MatchSnapshotReadStatus Status { get; }

    public MatchSnapshot? Snapshot { get; }

    public static MatchSnapshotReadResult Success(MatchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new MatchSnapshotReadResult(MatchSnapshotReadStatus.Success, snapshot);
    }

    public static MatchSnapshotReadResult NoActiveMatch() =>
        new(MatchSnapshotReadStatus.NoActiveMatch);

    public static MatchSnapshotReadResult GameLoading() =>
        new(MatchSnapshotReadStatus.GameLoading);

    public static MatchSnapshotReadResult Timeout() =>
        new(MatchSnapshotReadStatus.Timeout);

    public static MatchSnapshotReadResult InvalidData() =>
        new(MatchSnapshotReadStatus.InvalidData);

    public static MatchSnapshotReadResult Failed() =>
        new(MatchSnapshotReadStatus.Failed);
}
