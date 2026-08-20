namespace TempoCue.Vision;

/// <summary>
/// Хранит редкий диагностический снимок области миникарты в формате BGRA32.
/// Массив после публикации не изменяется и не сохраняется на диск.
/// </summary>
public sealed record MinimapPreviewSnapshot(
    int X,
    int Y,
    int RegionWidth,
    int RegionHeight,
    int ImageWidth,
    int ImageHeight,
    int ImageStride,
    byte[] BgraPixels,
    DateTimeOffset CapturedAt,
    MinimapScreenCorner Corner,
    double DetectionConfidence)
{
    /// <summary>
    /// Предварительные цветные контуры. Коллекция не содержит подтверждённых игровых фактов.
    /// </summary>
    public IReadOnlyList<MinimapMarkerCandidate> MarkerCandidates { get; init; } =
        Array.Empty<MinimapMarkerCandidate>();
}
