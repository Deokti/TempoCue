using TempoCue.Overlay;

namespace TempoCue.App.Settings;

/// <summary>Сохраняемые настройки оверлея и подачи советов.</summary>
public sealed record OverlayUserSettings
{
    public bool IsEnabled { get; init; } = true;

    public int Width { get; init; } = (int)OverlayPresentationSettings.DefaultWidth;

    public int BackgroundOpacityPercent { get; init; } = 80;

    public OverlayDurationMode DurationMode { get; init; } = OverlayDurationMode.Automatic;

    public int FixedDurationSeconds { get; init; } = OverlayPresentationSettings.DefaultFixedDurationSeconds;

    public PersistentAdviceFrequency PersistentAdviceFrequency { get; init; } =
        PersistentAdviceFrequency.OncePerMatch;

    public double? HorizontalPosition { get; init; }

    public double? VerticalPosition { get; init; }

    public OverlayUserSettings Normalize()
    {
        var presentation = ToPresentationSettings().Normalize();
        return this with
        {
            Width = (int)Math.Round(presentation.Width),
            BackgroundOpacityPercent = (int)Math.Round(presentation.BackgroundOpacity * 100),
            FixedDurationSeconds = presentation.FixedDurationSeconds,
            HorizontalPosition = presentation.HorizontalPosition,
            VerticalPosition = presentation.VerticalPosition
        };
    }

    public OverlayPresentationSettings ToPresentationSettings() => new()
    {
        IsEnabled = IsEnabled,
        Width = Width,
        BackgroundOpacity = BackgroundOpacityPercent / 100d,
        DurationMode = DurationMode,
        FixedDurationSeconds = FixedDurationSeconds,
        HorizontalPosition = HorizontalPosition,
        VerticalPosition = VerticalPosition
    };

    public OverlayUserSettings WithPlacement(OverlayPresentationSettings presentation) => this with
    {
        HorizontalPosition = presentation.HorizontalPosition,
        VerticalPosition = presentation.VerticalPosition
    };
}
