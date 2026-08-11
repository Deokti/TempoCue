namespace TempoCue.Application;

public sealed class ActiveMatchUpdateEventArgs(ActiveMatchUpdate update) : EventArgs
{
    public ActiveMatchUpdate Update { get; } =
        update ?? throw new ArgumentNullException(nameof(update));
}
