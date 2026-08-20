namespace TempoCue.Application;

/// <summary>Порт чтения безопасного состояния локального Champion Select.</summary>
public interface IChampionSelectSource
{
    Task<ChampionSelectReadResult> ReadAsync(
        CancellationToken cancellationToken = default);
}
