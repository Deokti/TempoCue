namespace TempoCue.Vision;

/// <summary>Неизменяемый результат распознавания одного канонического кадра миникарты.</summary>
public sealed class MinimapChampionObservationEventArgs : EventArgs
{
    public MinimapChampionObservationEventArgs(
        DateTimeOffset capturedAt,
        int imageWidth,
        int imageHeight,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates,
        bool isSuccessful = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
        ArgumentNullException.ThrowIfNull(candidates);
        if (!isSuccessful && candidates.Count > 0)
        {
            throw new ArgumentException(
                "Неудачное чтение кадра не может содержать кандидатов.",
                nameof(candidates));
        }

        CapturedAt = capturedAt;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        Candidates = candidates.ToArray();
        IsSuccessful = isSuccessful;
    }

    public DateTimeOffset CapturedAt { get; }

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    public IReadOnlyList<MinimapChampionMarkerCandidate> Candidates { get; }

    /// <summary>
    /// Отличает успешно прочитанный кадр от ошибки чтения. Неудачный кадр
    /// старит прежнюю видимость, но не подтверждает работоспособность потока.
    /// </summary>
    public bool IsSuccessful { get; }
}
