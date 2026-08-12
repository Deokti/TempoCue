namespace TempoCue.Overlay;

/// <summary>Карточка, занявшая один из двух видимых слотов.</summary>
public sealed record OverlayDisplayItem(
    long Sequence,
    OverlayNotification Notification,
    DateTimeOffset ShownAt,
    DateTimeOffset? ExpiresAt);
