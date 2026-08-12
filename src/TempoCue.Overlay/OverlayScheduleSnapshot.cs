namespace TempoCue.Overlay;

/// <summary>Неизменяемый снимок текущих карточек в порядке сверху вниз.</summary>
public sealed record OverlayScheduleSnapshot(
    IReadOnlyList<OverlayDisplayItem> Items,
    bool HasPending = false)
{
    public static OverlayScheduleSnapshot Empty { get; } = new([]);
}
