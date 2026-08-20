using System.Windows.Media;

namespace TempoCue.App.ViewModels;

/// <summary>Проецирует диагностическую рамку в каноническую систему координат миникарты.</summary>
public sealed record MinimapMarkerOverlayViewModel(
    int X,
    int Y,
    int Width,
    int Height,
    Brush Stroke,
    string? ChampionId,
    double IdentityConfidence);
