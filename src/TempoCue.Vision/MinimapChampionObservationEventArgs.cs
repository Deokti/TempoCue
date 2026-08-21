namespace TempoCue.Vision;

/// <summary>Неизменяемый результат распознавания одного канонического кадра миникарты.</summary>
public sealed class MinimapChampionObservationEventArgs : EventArgs
{
    public MinimapChampionObservationEventArgs(
        DateTimeOffset capturedAt,
        int imageWidth,
        int imageHeight,
        IReadOnlyList<MinimapChampionMarkerCandidate> candidates)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(imageHeight);
        ArgumentNullException.ThrowIfNull(candidates);
        CapturedAt = capturedAt;
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        Candidates = candidates.ToArray();
    }

    public DateTimeOffset CapturedAt { get; }

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    public IReadOnlyList<MinimapChampionMarkerCandidate> Candidates { get; }
}
