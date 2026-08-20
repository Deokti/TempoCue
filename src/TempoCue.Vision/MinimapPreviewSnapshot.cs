namespace TempoCue.Vision;

/// <summary>
/// Хранит редкий диагностический снимок области миникарты в формате BGRA32.
/// Массив после публикации не изменяется и не сохраняется на диск.
/// </summary>
public sealed record MinimapPreviewSnapshot(
    int X,
    int Y,
    int Width,
    int Height,
    int Stride,
    byte[] BgraPixels,
    DateTimeOffset CapturedAt,
    MinimapScreenCorner Corner,
    double DetectionConfidence);
