namespace TempoCue.Vision;

/// <summary>
/// Создаёт самодостаточную копию последнего диагностического кадра только по явному запросу.
/// </summary>
public interface IMinimapReplaySampleSource
{
    bool HasReplaySample { get; }

    MinimapReplaySample? CreateReplaySample();

    /// <summary>
    /// Идёт ли сейчас запись последовательности и сколько кадров уже собрано.
    /// Одиночный кадр не отвечает на вопрос об устойчивости распознавания между
    /// кадрами, поэтому нужна запись нескольких подряд.
    /// </summary>
    bool IsRecordingSequence { get; }

    int RecordedFrameCount { get; }

    /// <summary>
    /// Начинает сбор кадров. Запись идёт с той же частотой, с какой работает
    /// разбор миникарты, и останавливается сама, набрав нужное число кадров.
    /// </summary>
    void StartSequenceRecording(int frameCount);

    /// <summary>
    /// Забирает готовую запись и освобождает буфер. Возвращает null, пока кадров
    /// не набрано.
    /// </summary>
    MinimapReplaySequence? TakeRecordedSequence();
}
