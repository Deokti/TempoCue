namespace TempoCue.Vision;

/// <summary>
/// Создаёт самодостаточную копию последнего диагностического кадра только по явному запросу.
/// </summary>
public interface IMinimapReplaySampleSource
{
    bool HasReplaySample { get; }

    MinimapReplaySample? CreateReplaySample();
}
