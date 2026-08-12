namespace TempoCue.Overlay;

/// <summary>Проверенные параметры отображения, не зависящие от источника уведомлений.</summary>
public sealed record OverlayPresentationSettings
{
    public const double DefaultWidth = 340;
    public const double DefaultBackgroundOpacity = 0.8;
    public const int DefaultFixedDurationSeconds = 8;

    public bool IsEnabled { get; init; } = true;

    public double Width { get; init; } = DefaultWidth;

    public double BackgroundOpacity { get; init; } = DefaultBackgroundOpacity;

    public OverlayDurationMode DurationMode { get; init; } = OverlayDurationMode.Automatic;

    public int FixedDurationSeconds { get; init; } = DefaultFixedDurationSeconds;

    public double? HorizontalPosition { get; init; }

    public double? VerticalPosition { get; init; }

    public OverlayPresentationSettings Normalize() => this with
    {
        Width = Math.Clamp(Width, 300, 400),
        BackgroundOpacity = Math.Clamp(BackgroundOpacity, 0.6, 1),
        FixedDurationSeconds = Math.Clamp(FixedDurationSeconds, 5, 20),
        HorizontalPosition = NormalizePosition(HorizontalPosition),
        VerticalPosition = NormalizePosition(VerticalPosition)
    };

    private static double? NormalizePosition(double? value) =>
        value is null || !double.IsFinite(value.Value)
            ? null
            : Math.Clamp(value.Value, 0, 1);
}
