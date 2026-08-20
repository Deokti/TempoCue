namespace TempoCue.Vision;

/// <summary>Описывает прямоугольную область кадра в физических пикселях.</summary>
internal readonly record struct PixelRegion(int X, int Y, int Width, int Height);
