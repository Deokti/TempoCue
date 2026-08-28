namespace TempoCue.Vision;

/// <summary>
/// Описывает ещё не подтверждённую метку в канонических координатах миникарты.
/// Кандидат не является игровым фактом и не содержит предположения о чемпионе.
/// </summary>
public sealed record MinimapMarkerCandidate(
    int X,
    int Y,
    int Width,
    int Height,
    MinimapMarkerAffiliation Affiliation,
    double Confidence);
