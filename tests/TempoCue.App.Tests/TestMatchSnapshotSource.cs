using TempoCue.Application;

namespace TempoCue.App.Tests;

/// <summary>Создаёт управляемый источник снимков без сети и запущенного клиента League.</summary>
internal sealed class TestMatchSnapshotSource : IMatchSnapshotSource
{
    private readonly Func<CancellationToken, Task<MatchSnapshotReadResult>> _read;

    public TestMatchSnapshotSource(
        Func<CancellationToken, Task<MatchSnapshotReadResult>> read)
    {
        _read = read ?? throw new ArgumentNullException(nameof(read));
    }

    public static TestMatchSnapshotSource FromResult(MatchSnapshotReadResult result) =>
        new(_ => Task.FromResult(result));

    public static TestMatchSnapshotSource FromSnapshot(MatchSnapshot snapshot) =>
        FromResult(MatchSnapshotReadResult.Success(snapshot));

    public static TestMatchSnapshotSource FromSequence(
        params MatchSnapshotReadResult[] results)
    {
        var remaining = new Queue<MatchSnapshotReadResult>(results);
        return new(_ => Task.FromResult(remaining.Dequeue()));
    }

    public Task<MatchSnapshotReadResult> ReadAsync(
        CancellationToken cancellationToken = default) =>
        _read(cancellationToken);
}
