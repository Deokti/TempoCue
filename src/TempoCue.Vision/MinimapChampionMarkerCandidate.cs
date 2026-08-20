namespace TempoCue.Vision;

/// <summary>
/// Цветовой контур, форма и содержимое которого похожи на портрет чемпиона.
/// Кандидат остаётся диагностическим наблюдением и не является игровым фактом.
/// </summary>
public sealed record MinimapChampionMarkerCandidate(
    int X,
    int Y,
    int Width,
    int Height,
    MinimapMarkerAffiliation Affiliation,
    double Confidence,
    string? ChampionId = null,
    double IdentityConfidence = 0);
