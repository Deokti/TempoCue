namespace TempoCue.Application;

/// <summary>Стабильное состояние видимости участника после объединения нескольких кадров.</summary>
public enum ParticipantVisibilityStatus
{
    /// <summary>
    /// Поток наблюдений отсутствует или прерван, поэтому видимость не известна.
    /// Трекер это состояние не возвращает: его назначает потребитель, который
    /// владеет знанием о жизни источника кадров.
    /// </summary>
    Unknown,
    NotVisible,
    Confirming,
    Visible
}
