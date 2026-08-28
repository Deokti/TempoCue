using System.Windows.Media;

namespace TempoCue.App.ViewModels;

/// <summary>Представляет один диагностический статус участника активного матча.</summary>
public sealed record VisionParticipantStatusViewModel(
    string ChampionName,
    string PositionCode,
    string StatusText,
    string TeamTraceText,
    Brush StatusBrush);
