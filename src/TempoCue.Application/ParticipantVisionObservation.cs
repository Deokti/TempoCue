namespace TempoCue.Application;

/// <summary>
/// Нормализованное наблюдение одного участника на одном кадре миникарты.
/// Координаты задаются в диапазоне от 0 до 1 и не зависят от размера изображения.
/// </summary>
public sealed record ParticipantVisionObservation
{
    public ParticipantVisionObservation(
        string participantId,
        double confidence,
        double mapX,
        double mapY)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(participantId);
        if (!double.IsFinite(confidence) || confidence is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence));
        }

        if (!double.IsFinite(mapX) || mapX is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mapX));
        }

        if (!double.IsFinite(mapY) || mapY is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(mapY));
        }

        ParticipantId = participantId;
        Confidence = confidence;
        MapX = mapX;
        MapY = mapY;
    }

    public string ParticipantId { get; }

    public double Confidence { get; }

    public double MapX { get; }

    public double MapY { get; }
}
