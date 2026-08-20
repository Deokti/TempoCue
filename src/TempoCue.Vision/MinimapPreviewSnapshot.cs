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

    /// <summary>Количество областей, переданных сопоставителю портретов.</summary>
    public int ChampionProposalCount { get; init; }

    /// <summary>
    /// Контуры, подтверждённые сравнением с портретами участников активного матча.
    /// </summary>
    public IReadOnlyList<MinimapChampionMarkerCandidate> ChampionMarkerCandidates { get; init; } =
        Array.Empty<MinimapChampionMarkerCandidate>();

    /// <summary>Количество портретов участников, доступных сопоставителю.</summary>
    public int ChampionReferenceCount { get; init; }

    /// <summary>Лучшее сходство кандидата с портретом до применения порога.</summary>
    public double MaximumIdentityConfidence { get; init; }
}
