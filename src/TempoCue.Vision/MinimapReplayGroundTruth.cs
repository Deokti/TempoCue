namespace TempoCue.Vision;

/// <summary>Вид размеченного вручную объекта на каноническом кадре миникарты.</summary>
public enum MinimapGroundTruthKind
{
    Champion,
    DeadChampion,
    Tower,
    MinionWave,
    Ward,
    Other
}

/// <summary>
/// Одна размеченная вручную область в канонической системе координат кадра.
/// Champion описывает настоящий значок чемпиона; остальные виды фиксируют
/// hard negative — объект, который распознаватель не должен подтверждать.
/// </summary>
public sealed record MinimapGroundTruthRegion
{
    public MinimapGroundTruthRegion(
        int x,
        int y,
        int width,
        int height,
        MinimapGroundTruthKind kind,
        string? championId = null,
        MinimapMarkerAffiliation? affiliation = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        // Разметка описывает канонический кадр: область за его границей исказила
        // бы метрики корпуса и поэтому отклоняется при создании.
        if (checked(x + width) > MinimapFrameNormalizer.CanonicalSize ||
            checked(y + height) > MinimapFrameNormalizer.CanonicalSize)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (kind == MinimapGroundTruthKind.Champion && string.IsNullOrWhiteSpace(championId))
        {
            throw new ArgumentException(
                "Разметка чемпиона обязана называть его идентификатор.",
                nameof(championId));
        }

        if (affiliation is { } value && !Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(nameof(affiliation));
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
        Kind = kind;
        ChampionId = string.IsNullOrWhiteSpace(championId) ? null : championId;
        Affiliation = affiliation;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public MinimapGroundTruthKind Kind { get; }

    public string? ChampionId { get; }

    public MinimapMarkerAffiliation? Affiliation { get; }

    public double CenterX => X + (Width / 2d);

    public double CenterY => Y + (Height / 2d);
}

/// <summary>
/// Ручная разметка одного replay-кадра. В отличие от baseline это утверждение
/// об истине, а допуски фиксируют известное текущее поведение распознавателя,
/// чтобы корпусные тесты падали только при его ухудшении.
/// </summary>
public sealed record MinimapReplayGroundTruth
{
    private const int MaximumRegionCount = 1024;

    public MinimapReplayGroundTruth(
        IReadOnlyList<MinimapGroundTruthRegion> regions,
        int allowedFalsePositives = 0,
        int allowedMissedChampions = 0)
    {
        ArgumentNullException.ThrowIfNull(regions);
        if (regions.Count > MaximumRegionCount)
        {
            throw new ArgumentOutOfRangeException(nameof(regions));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(allowedFalsePositives);
        ArgumentOutOfRangeException.ThrowIfNegative(allowedMissedChampions);

        Regions = regions.ToArray();
        AllowedFalsePositives = allowedFalsePositives;
        AllowedMissedChampions = allowedMissedChampions;
    }

    public IReadOnlyList<MinimapGroundTruthRegion> Regions { get; }

    public int AllowedFalsePositives { get; }

    public int AllowedMissedChampions { get; }
}
