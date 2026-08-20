namespace TempoCue.Vision;

/// <summary>Результат поиска прямоугольной границы миникарты в кадре.</summary>
internal sealed record MinimapRegionDetection(
    PixelRegion Region,
    MinimapScreenCorner Corner,
    double Confidence,
    double BoundaryScore);
